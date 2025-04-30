#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlayerHealthManager))]
public class PlayerHealthManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        // 获取目标组件
        PlayerHealthManager healthManager = (PlayerHealthManager)target;
        
        // 添加分隔线
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();
        
        // 添加标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("调试工具", titleStyle);
        
        EditorGUILayout.Space();
        
        // 显示当前生命值信息（只在运行时显示）
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前生命值:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{healthManager.GetCurrentHealth()} / {healthManager.GetMaxHealth()}");
            EditorGUILayout.EndHorizontal();
            
            // 显示无敌状态
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("无敌状态:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(healthManager.IsInvincible() ? "是" : "否");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        // 创建按钮行
        EditorGUILayout.BeginHorizontal();
        
        // 立即杀死玩家按钮
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f); // 红色背景
        if (GUILayout.Button(new GUIContent("立即杀死玩家", "将玩家生命值设为0并触发死亡序列"), GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                healthManager.KillPlayer();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行时杀死玩家。", "确定");
            }
        }
        
        GUI.backgroundColor = Color.white; // 恢复默认背景色
        
        EditorGUILayout.EndHorizontal();
        
        // 添加更多调试按钮
        EditorGUILayout.BeginHorizontal();
        
        // 减少生命值按钮
        if (GUILayout.Button(new GUIContent("减少1点生命值", "对玩家造成1点伤害"), GUILayout.Height(25)))
        {
            if (Application.isPlaying)
            {
                GameEvents.TriggerPlayerDamaged(1);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行时减少生命值。", "确定");
            }
        }
        
        // 恢复生命值按钮
        GUI.backgroundColor = new Color(0.3f, 1f, 0.3f); // 绿色背景
        if (GUILayout.Button(new GUIContent("完全恢复生命值", "将玩家生命值恢复到最大值"), GUILayout.Height(25)))
        {
            if (Application.isPlaying)
            {
                healthManager.FullHeal();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行时恢复生命值。", "确定");
            }
        }
        GUI.backgroundColor = Color.white; // 恢复默认背景色
        
        EditorGUILayout.EndHorizontal();
        
        // 添加提示
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("这些功能只在游戏运行时可用。", MessageType.Info);
        }
    }
}
#endif