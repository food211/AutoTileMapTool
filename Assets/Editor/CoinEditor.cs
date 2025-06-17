using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(Coin))]
public class CoinEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        Coin coin = (Coin)target;
        SerializedProperty idProperty = serializedObject.FindProperty("coinID");
        
        if (GUILayout.Button("生成唯一ID"))
        {
            // 生成基于场景和位置的ID
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Vector3 pos = coin.transform.position;
            string newID = $"{sceneName}_Coin_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
            
            idProperty.stringValue = newID;
            serializedObject.ApplyModifiedProperties();
            
            Debug.LogFormat($"已为金币生成ID: {newID}");
        }
        
        // 添加测试按钮
        if (!string.IsNullOrEmpty(idProperty.stringValue))
        {
            if (GUILayout.Button("测试 - 标记为已收集"))
            {
                // 确保ProgressManager实例存在
                ProgressManager manager = FindObjectOfType<ProgressManager>();
                if (manager != null)
                {
                    manager.SaveCoinCollected(idProperty.stringValue);
                    Debug.LogFormat($"已将金币 {idProperty.stringValue} 标记为已收集");
                }
                else
                {
                    Debug.LogError("未找到ProgressManager实例，无法标记金币状态");
                }
            }
            
            if (GUILayout.Button("测试 - 标记为未收集"))
            {
                // 确保ProgressManager实例存在
                ProgressManager manager = FindObjectOfType<ProgressManager>();
                if (manager != null)
                {
                    PlayerData data = manager.LoadPlayerData();
                    if (data.collectedCoins.Contains(idProperty.stringValue))
                    {
                        data.collectedCoins.Remove(idProperty.stringValue);
                        manager.SavePlayerData();
                        Debug.LogFormat($"已将金币 {idProperty.stringValue} 标记为未收集");
                    }
                }
                else
                {
                    Debug.LogError("未找到ProgressManager实例，无法标记金币状态");
                }
            }
            
            if (GUILayout.Button("测试 - 检查收集状态"))
            {
                // 确保ProgressManager实例存在
                ProgressManager manager = FindObjectOfType<ProgressManager>();
                if (manager != null)
                {
                    bool isCollected = manager.IsCoinCollected(idProperty.stringValue);
                    Debug.LogFormat($"金币 {idProperty.stringValue} 状态: {(isCollected ? "已收集" : "未收集")}");
                }
                else
                {
                    Debug.LogError("未找到ProgressManager实例，无法检查金币状态");
                }
            }
        }
    }

    // 添加菜单项到Tools菜单
    [MenuItem("Tools/金币工具/为所有金币生成唯一ID")]
    public static void GenerateAllCoinIDs()
    {
        // 查找场景中所有的金币
        Coin[] allCoins = GameObject.FindObjectsOfType<Coin>();
        
        if (allCoins.Length == 0)
        {
            EditorUtility.DisplayDialog("金币工具", "场景中没有找到金币对象！", "确定");
            return;
        }
        
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int updatedCount = 0;
        List<string> generatedIDs = new List<string>();
        
        // 开始记录撤销操作
        Undo.RecordObjects(allCoins, "Generate Coin IDs");
        
        // 为每个金币生成ID
        foreach (Coin coin in allCoins)
        {
            // 获取SerializedObject以便修改属性
            SerializedObject serializedCoin = new SerializedObject(coin);
            SerializedProperty idProperty = serializedCoin.FindProperty("coinID");
            
            // 如果ID为空，则生成新ID
            if (string.IsNullOrEmpty(idProperty.stringValue))
            {
                Vector3 pos = coin.transform.position;
                string newID = $"{sceneName}_Coin_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
                
                // 确保ID唯一
                int counter = 1;
                string baseID = newID;
                while (generatedIDs.Contains(newID))
                {
                    newID = $"{baseID}_{counter}";
                    counter++;
                }
                
                idProperty.stringValue = newID;
                serializedCoin.ApplyModifiedProperties();
                
                generatedIDs.Add(newID);
                updatedCount++;
            }
            else
            {
                generatedIDs.Add(idProperty.stringValue);
            }
        }
        
        // 标记场景为已修改
        if (updatedCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
        
        EditorUtility.DisplayDialog("金币工具", 
            $"处理完成!\n\n共找到 {allCoins.Length} 个金币\n已更新 {updatedCount} 个金币ID", 
            "确定");
        
        Debug.Log($"金币ID生成完成: 共找到 {allCoins.Length} 个金币，已更新 {updatedCount} 个金币ID");
    }
    
    // 添加菜单项检查金币ID是否有重复
    [MenuItem("Tools/金币工具/检查金币ID重复")]
    public static void CheckDuplicateCoinIDs()
    {
        Coin[] allCoins = GameObject.FindObjectsOfType<Coin>();
        
        if (allCoins.Length == 0)
        {
            EditorUtility.DisplayDialog("金币工具", "场景中没有找到金币对象！", "确定");
            return;
        }
        
        Dictionary<string, List<Coin>> idMap = new Dictionary<string, List<Coin>>();
        int emptyCount = 0;
        
        // 收集所有ID
        foreach (Coin coin in allCoins)
        {
            SerializedObject serializedCoin = new SerializedObject(coin);
            SerializedProperty idProperty = serializedCoin.FindProperty("coinID");
            
            string id = idProperty.stringValue;
            
            if (string.IsNullOrEmpty(id))
            {
                emptyCount++;
                continue;
            }
            
            if (!idMap.ContainsKey(id))
            {
                idMap[id] = new List<Coin>();
            }
            
            idMap[id].Add(coin);
        }
        
        // 检查重复
        List<string> duplicateIDs = new List<string>();
        foreach (var pair in idMap)
        {
            if (pair.Value.Count > 1)
            {
                duplicateIDs.Add(pair.Key);
            }
        }
        
        // 显示结果
        if (duplicateIDs.Count > 0 || emptyCount > 0)
        {
            string message = "";
            
            if (duplicateIDs.Count > 0)
            {
                message += $"发现 {duplicateIDs.Count} 个重复的金币ID:\n\n";
                
                foreach (string id in duplicateIDs)
                {
                    message += $"ID \"{id}\" 被使用了 {idMap[id].Count} 次\n";
                    
                    // 输出到控制台以便点击定位
                    Debug.LogWarning($"金币ID \"{id}\" 重复使用了 {idMap[id].Count} 次:");
                    foreach (var coin in idMap[id])
                    {
                        Debug.LogWarning($"  - {coin.name} 在位置 {coin.transform.position}", coin);
                    }
                }
            }
            
            if (emptyCount > 0)
            {
                if (message != "") message += "\n";
                message += $"发现 {emptyCount} 个金币没有设置ID";
            }
            
            EditorUtility.DisplayDialog("金币ID检查结果", message, "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("金币ID检查结果", 
                $"很好! 所有 {allCoins.Length} 个金币都有唯一的ID。", 
                "确定");
        }
    }
    
    // 添加菜单项强制更新所有金币ID
    [MenuItem("Tools/金币工具/强制更新所有金币ID")]
    public static void ForceUpdateAllCoinIDs()
    {
        // 查找场景中所有的金币
        Coin[] allCoins = GameObject.FindObjectsOfType<Coin>();
        
        if (allCoins.Length == 0)
        {
            EditorUtility.DisplayDialog("金币工具", "场景中没有找到金币对象！", "确定");
            return;
        }
        
        // 询问用户是否确定要强制更新所有ID
        bool proceed = EditorUtility.DisplayDialog("强制更新金币ID", 
            "此操作将重新生成所有金币的ID，可能会导致已保存的收集状态失效。\n\n确定要继续吗？", 
            "确定", "取消");
            
        if (!proceed)
        {
            return;
        }
        
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int updatedCount = 0;
        List<string> generatedIDs = new List<string>();
        
        // 开始记录撤销操作
        Undo.RecordObjects(allCoins, "Force Update Coin IDs");
        
        // 为每个金币生成新ID
        foreach (Coin coin in allCoins)
        {
            // 获取SerializedObject以便修改属性
            SerializedObject serializedCoin = new SerializedObject(coin);
            SerializedProperty idProperty = serializedCoin.FindProperty("coinID");
            
            // 生成新ID
            Vector3 pos = coin.transform.position;
            string newID = $"{sceneName}_Coin_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
            
            // 确保ID唯一
            int counter = 1;
            string baseID = newID;
            while (generatedIDs.Contains(newID))
            {
                newID = $"{baseID}_{counter}";
                counter++;
            }
            
            // 记录旧ID用于日志
            string oldID = idProperty.stringValue;
            
            // 设置新ID
            idProperty.stringValue = newID;
            serializedCoin.ApplyModifiedProperties();
            
            generatedIDs.Add(newID);
            updatedCount++;
            
            // 记录ID变更日志
            if (!string.IsNullOrEmpty(oldID) && oldID != newID)
            {
                Debug.Log($"金币ID已更新: {oldID} -> {newID}", coin);
            }
        }
        
        // 标记场景为已修改
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        EditorUtility.DisplayDialog("金币工具", 
            $"强制更新完成!\n\n共找到 {allCoins.Length} 个金币\n已更新 {updatedCount} 个金币ID", 
            "确定");
        
        Debug.Log($"金币ID强制更新完成: 共找到 {allCoins.Length} 个金币，已更新 {updatedCount} 个金币ID");
    }
}