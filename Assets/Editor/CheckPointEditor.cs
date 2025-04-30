#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Checkpoint))]
public class CheckpointEditor : Editor
{
    SerializedProperty checkpointIDProp;
    SerializedProperty isActiveProp;
    SerializedProperty healOnActivateProp;
    SerializedProperty respawnPointProp;
    
    SerializedProperty areaShapeProp;
    SerializedProperty activationRadiusProp;
    SerializedProperty activationSizeProp;
    SerializedProperty activationOffsetProp;
    
    SerializedProperty activeVisualProp;
    SerializedProperty inactiveVisualProp;
    SerializedProperty activationParticleProp;
    SerializedProperty pointLightProp;
    SerializedProperty activationAreaColorProp;
    SerializedProperty respawnPointColorProp;
    
    private bool showBasicSettings = true;
    private bool showActivationAreaSettings = true;
    private bool showVisualSettings = true;
    
    void OnEnable()
    {
        checkpointIDProp = serializedObject.FindProperty("checkpointID");
        isActiveProp = serializedObject.FindProperty("isActive");
        healOnActivateProp = serializedObject.FindProperty("HealOnActivate");
        respawnPointProp = serializedObject.FindProperty("respawnPoint");
        
        areaShapeProp = serializedObject.FindProperty("areaShape");
        activationRadiusProp = serializedObject.FindProperty("activationRadius");
        activationSizeProp = serializedObject.FindProperty("activationSize");
        activationOffsetProp = serializedObject.FindProperty("activationOffset");
        
        activeVisualProp = serializedObject.FindProperty("activeVisual");
        inactiveVisualProp = serializedObject.FindProperty("inactiveVisual");
        activationParticleProp = serializedObject.FindProperty("activationParticle");
        pointLightProp = serializedObject.FindProperty("pointLight");
        activationAreaColorProp = serializedObject.FindProperty("activationAreaColor");
        respawnPointColorProp = serializedObject.FindProperty("respawnPointColor");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        Checkpoint checkpoint = (Checkpoint)target;
        
        // 基本设置
        showBasicSettings = EditorGUILayout.Foldout(showBasicSettings, "基本设置", true, EditorStyles.foldoutHeader);
        if (showBasicSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(checkpointIDProp, new GUIContent("存档点ID", "存档点的唯一标识符，如果为空则使用游戏对象名称"));
            EditorGUILayout.PropertyField(isActiveProp, new GUIContent("是否激活", "存档点当前是否处于激活状态"));
            EditorGUILayout.PropertyField(healOnActivateProp, new GUIContent("激活时恢复生命值", "玩家激活此存档点时是否恢复生命值"));
            EditorGUILayout.PropertyField(respawnPointProp, new GUIContent("重生点", "玩家重生的位置，如果为空则使用存档点自身位置"));
            
            EditorGUI.indentLevel--;
        }
        
        // 激活区域设置
        showActivationAreaSettings = EditorGUILayout.Foldout(showActivationAreaSettings, "激活区域设置", true, EditorStyles.foldoutHeader);
        if (showActivationAreaSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(areaShapeProp, new GUIContent("区域形状", "激活区域的形状"));
            
            Checkpoint.ActivationAreaShape shape = (Checkpoint.ActivationAreaShape)areaShapeProp.enumValueIndex;
            
            if (shape == Checkpoint.ActivationAreaShape.Circle)
            {
                EditorGUILayout.PropertyField(activationRadiusProp, new GUIContent("激活半径", "圆形激活区域的半径"));
            }
            else
            {
                EditorGUILayout.PropertyField(activationSizeProp, new GUIContent("激活区域大小", "矩形激活区域的大小"));
            }
            
            EditorGUILayout.PropertyField(activationOffsetProp, new GUIContent("激活区域偏移", "相对于存档点的位置偏移"));
            
            EditorGUI.indentLevel--;
        }
        
        // 视觉效果设置
        showVisualSettings = EditorGUILayout.Foldout(showVisualSettings, "视觉效果设置", true, EditorStyles.foldoutHeader);
        if (showVisualSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(activeVisualProp, new GUIContent("激活视觉效果", "存档点激活时显示的视觉效果"));
            EditorGUILayout.PropertyField(inactiveVisualProp, new GUIContent("未激活视觉效果", "存档点未激活时显示的视觉效果"));
            EditorGUILayout.PropertyField(activationParticleProp, new GUIContent("激活粒子效果", "存档点激活时播放的粒子效果"));
            EditorGUILayout.PropertyField(pointLightProp, new GUIContent("点光源", "存档点的点光源组件"));
            
            EditorGUILayout.PropertyField(activationAreaColorProp, new GUIContent("激活区域颜色", "在Scene视图中显示的激活区域颜色"));
            EditorGUILayout.PropertyField(respawnPointColorProp, new GUIContent("重生点颜色", "在Scene视图中显示的重生点颜色"));
            
            EditorGUI.indentLevel--;
        }
        
        serializedObject.ApplyModifiedProperties();
        
        // 添加实用按钮
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("创建重生点"))
        {
            CreateRespawnPoint(checkpoint);
        }
        
        if (GUILayout.Button("更新激活区域"))
        {
            if (Application.isPlaying)
            {
                checkpoint.UpdateActivationArea();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行时更新激活区域。", "确定");
            }
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void CreateRespawnPoint(Checkpoint checkpoint)
    {
        // 创建重生点游戏对象
        GameObject respawnObj = new GameObject(checkpoint.name + "_RespawnPoint");
        respawnObj.transform.position = checkpoint.transform.position;
        respawnObj.transform.SetParent(checkpoint.transform);
        
        // 将重生点设置为存档点的重生点
        SerializedProperty respawnPointProp = serializedObject.FindProperty("respawnPoint");
        respawnPointProp.objectReferenceValue = respawnObj.transform;
        serializedObject.ApplyModifiedProperties();
        
        // 选中新创建的重生点
        Selection.activeGameObject = respawnObj;
        
        EditorUtility.DisplayDialog("成功", "已创建重生点，现在您可以调整其位置。", "确定");
    }
    
    // 添加Scene视图控制
    private void OnSceneGUI()
    {
        Checkpoint checkpoint = (Checkpoint)target;
        
        // 如果有单独的重生点，添加位置控制手柄
        if (checkpoint.RespawnPoint != null && checkpoint.RespawnPoint != checkpoint.transform)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(checkpoint.RespawnPoint.position, checkpoint.RespawnPoint.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(checkpoint.RespawnPoint, "Move Respawn Point");
                checkpoint.RespawnPoint.position = newPosition;
            }
        }
        
        // 添加激活区域偏移控制
        EditorGUI.BeginChangeCheck();
        Vector3 areaPosition = checkpoint.transform.position + new Vector3(checkpoint.GetComponent<Checkpoint>().activationOffset.x, checkpoint.GetComponent<Checkpoint>().activationOffset.y, 0);
        Vector3 newAreaPosition = Handles.PositionHandle(areaPosition, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(checkpoint, "Move Activation Area");
            SerializedProperty offsetProp = serializedObject.FindProperty("activationOffset");
            offsetProp.vector2Value = new Vector2(newAreaPosition.x - checkpoint.transform.position.x, newAreaPosition.y - checkpoint.transform.position.y);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif