#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameUI_HealthManager))]
public class GameUI_HealthManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GameUI_HealthManager healthManager = (GameUI_HealthManager)target;
        var player = FindObjectOfType<PlayerHealthManager>();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("测试功能", EditorStyles.boldLabel);
        
        // 添加测试按钮
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("伤害 -1"))
            {
                GameEvents.TriggerPlayerDamaged(1);
            }
            
            if (GUILayout.Button("恢复 +1"))
            {
                // 这里需要获取当前玩家的生命值               
                if (player != null)
                {
                    player.Heal(1);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("添加护盾"))
            {
                player.AddShield(1);
            }
            
            if (GUILayout.Button("移除护盾"))
            {
                player.RemoveShield(1);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("限制生命值显示为3"))
            {
                healthManager.SetMaxDisplayHealth(3);
            }
            
            if (GUILayout.Button("重置生命值显示"))
            {
                healthManager.ResetMaxDisplayHealth();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("进入播放模式以使用测试功能", MessageType.Info);
        }
    }
}
#endif