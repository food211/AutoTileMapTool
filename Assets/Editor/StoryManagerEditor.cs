using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(StoryManager))]
public class StoryManagerEditor : Editor
{
    private SerializedProperty storySequenceIdProp;
    private SerializedProperty storyActionsProp;
    private SerializedProperty playOnStartProp;
    private SerializedProperty resetCameraOnCompleteProp;
    private SerializedProperty onStorySequenceStartedProp;
    private SerializedProperty onStorySequenceCompletedProp;

    private void OnEnable()
    {
        storySequenceIdProp = serializedObject.FindProperty("storySequenceId");
        storyActionsProp = serializedObject.FindProperty("storyActions");
        playOnStartProp = serializedObject.FindProperty("playOnStart");
        resetCameraOnCompleteProp = serializedObject.FindProperty("resetCameraOnComplete");
        onStorySequenceStartedProp = serializedObject.FindProperty("onStorySequenceStarted");
        onStorySequenceCompletedProp = serializedObject.FindProperty("onStorySequenceCompleted");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        // 添加提示信息
        EditorGUILayout.HelpBox("此StoryManager始终使用Activate激活类型", MessageType.Info);
        // 绘制基类属性
        DrawPropertiesExcluding(serializedObject, "uniqueID","activationType","storySequenceId", "storyActions", "playOnStart", "resetCameraOnComplete", "onStorySequenceStarted", "onStorySequenceCompleted");

        // 绘制故事序列ID
        EditorGUILayout.PropertyField(storySequenceIdProp);

        // 绘制播放选项
        EditorGUILayout.PropertyField(playOnStartProp);
        EditorGUILayout.PropertyField(resetCameraOnCompleteProp);

        // 绘制故事动作列表
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("故事动作序列", EditorStyles.boldLabel);

        // 添加新动作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加黑边显示"))
        {
            AddStoryAction(StoryManager.StoryActionType.ShowLetterbox);
        }
        if (GUILayout.Button("添加黑边隐藏"))
        {
            AddStoryAction(StoryManager.StoryActionType.HideLetterbox);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加兴趣点"))
        {
            AddStoryAction(StoryManager.StoryActionType.FollowPoint);
        }
        if (GUILayout.Button("添加缩放"))
        {
            AddStoryAction(StoryManager.StoryActionType.ZoomCamera);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加重置相机"))
        {
            AddStoryAction(StoryManager.StoryActionType.ResetCamera);
        }
        if (GUILayout.Button("添加等待"))
        {
            AddStoryAction(StoryManager.StoryActionType.Wait);
        }
        EditorGUILayout.EndHorizontal();

        // 绘制故事动作列表
        for (int i = 0; i < storyActionsProp.arraySize; i++)
        {
            EditorGUILayout.Space();
            
            SerializedProperty actionProp = storyActionsProp.GetArrayElementAtIndex(i);
            SerializedProperty actionTypeProp = actionProp.FindPropertyRelative("actionType");
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 动作标题和删除按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"动作 {i+1}: {actionTypeProp.enumDisplayNames[actionTypeProp.enumValueIndex]}", EditorStyles.boldLabel);
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                storyActionsProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.EndHorizontal();
            
            // 绘制动作类型
            EditorGUILayout.PropertyField(actionTypeProp);
            
            // 绘制持续时间
            SerializedProperty durationProp = actionProp.FindPropertyRelative("duration");
            EditorGUILayout.PropertyField(durationProp);
            
            // 根据动作类型绘制不同的属性
            StoryManager.StoryActionType actionType = (StoryManager.StoryActionType)actionTypeProp.enumValueIndex;
            
            switch (actionType)
            {
                case StoryManager.StoryActionType.ShowLetterbox:
                    SerializedProperty letterboxHeightProp = actionProp.FindPropertyRelative("letterboxHeight");
                    EditorGUILayout.PropertyField(letterboxHeightProp, new GUIContent("黑边高度"));
                    break;
                    
                case StoryManager.StoryActionType.FollowPoint:
                    SerializedProperty useTargetObjectProp = actionProp.FindPropertyRelative("useTargetObject");
                    EditorGUILayout.PropertyField(useTargetObjectProp, new GUIContent("使用目标对象"));
                    
                    if (useTargetObjectProp.boolValue)
                    {
                        SerializedProperty targetObjectProp = actionProp.FindPropertyRelative("targetObject");
                        EditorGUILayout.PropertyField(targetObjectProp, new GUIContent("目标对象"));
                        
                        SerializedProperty dynamicTrackingProp = actionProp.FindPropertyRelative("dynamicTracking");
                        EditorGUILayout.PropertyField(dynamicTrackingProp, new GUIContent("动态跟踪"));
                    }
                    else
                    {
                        SerializedProperty positionProp = actionProp.FindPropertyRelative("position");
                        EditorGUILayout.PropertyField(positionProp, new GUIContent("位置"));
                    }
                    
                    SerializedProperty zoomSizeProp = actionProp.FindPropertyRelative("zoomSize");
                    EditorGUILayout.PropertyField(zoomSizeProp, new GUIContent("缩放大小"));
                    break;
                    
                case StoryManager.StoryActionType.ZoomCamera:
                    SerializedProperty zoomSizeProp2 = actionProp.FindPropertyRelative("zoomSize");
                    EditorGUILayout.PropertyField(zoomSizeProp2, new GUIContent("缩放大小"));
                    break;
            }
            
            EditorGUILayout.EndVertical();
        }

        // 绘制事件
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("事件", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(onStorySequenceStartedProp);
        EditorGUILayout.PropertyField(onStorySequenceCompletedProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void AddStoryAction(StoryManager.StoryActionType actionType)
    {
        int index = storyActionsProp.arraySize;
        storyActionsProp.InsertArrayElementAtIndex(index);
        SerializedProperty newAction = storyActionsProp.GetArrayElementAtIndex(index);
        
        newAction.FindPropertyRelative("actionType").enumValueIndex = (int)actionType;
        
        // 设置默认值
        switch (actionType)
        {
            case StoryManager.StoryActionType.ShowLetterbox:
                newAction.FindPropertyRelative("duration").floatValue = 1.0f;
                newAction.FindPropertyRelative("letterboxHeight").floatValue = 0.1f;
                break;
                
            case StoryManager.StoryActionType.HideLetterbox:
                newAction.FindPropertyRelative("duration").floatValue = 1.0f;
                break;
                
            case StoryManager.StoryActionType.FollowPoint:
                newAction.FindPropertyRelative("duration").floatValue = 2.0f;
                newAction.FindPropertyRelative("useTargetObject").boolValue = false;
                newAction.FindPropertyRelative("dynamicTracking").boolValue = true;
                newAction.FindPropertyRelative("zoomSize").floatValue = 5.0f;
                break;
                
            case StoryManager.StoryActionType.ZoomCamera:
                newAction.FindPropertyRelative("duration").floatValue = 1.0f;
                newAction.FindPropertyRelative("zoomSize").floatValue = 5.0f;
                break;
                
            case StoryManager.StoryActionType.ResetCamera:
                newAction.FindPropertyRelative("duration").floatValue = 1.0f;
                break;
                
            case StoryManager.StoryActionType.Wait:
                newAction.FindPropertyRelative("duration").floatValue = 1.0f;
                break;
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}