#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

[CustomEditor(typeof(SceneReferenceResolver))]
public class SceneReferenceResolverEditor : Editor
{
    private SceneReferenceResolver resolver;
    private SerializedProperty managerReferencesProperty;
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
    private Dictionary<string, bool> fieldFoldoutStates = new Dictionary<string, bool>();
    
    private void OnEnable()
    {
        resolver = (SceneReferenceResolver)target;
        managerReferencesProperty = serializedObject.FindProperty("managerReferences");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // 绘制调试设置
        DrawPropertiesExcluding(serializedObject, "managerReferences");
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("管理器引用设置", EditorStyles.boldLabel);
        
        // 绘制管理器引用列表
        for (int i = 0; i < managerReferencesProperty.arraySize; i++)
        {
            SerializedProperty managerRefProp = managerReferencesProperty.GetArrayElementAtIndex(i);
            SerializedProperty targetManagerObjectProp = managerRefProp.FindPropertyRelative("targetManagerObject");
            SerializedProperty managerTypeNameProp = managerRefProp.FindPropertyRelative("managerTypeName");
            SerializedProperty fieldReferencesProp = managerRefProp.FindPropertyRelative("fieldReferences");
            
            string managerKey = $"manager_{i}";
            if (!foldoutStates.ContainsKey(managerKey))
            {
                foldoutStates[managerKey] = true;
            }
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // 管理器标题行
            EditorGUILayout.BeginHorizontal();
            foldoutStates[managerKey] = EditorGUILayout.Foldout(foldoutStates[managerKey], 
                $"管理器 {i + 1}: {(targetManagerObjectProp.objectReferenceValue != null ? targetManagerObjectProp.objectReferenceValue.name : "未选择")}");
            
            // 删除按钮
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                managerReferencesProperty.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.EndHorizontal();
            
            if (foldoutStates[managerKey])
            {
                EditorGUI.indentLevel++;
                
                // 管理器对象字段
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(targetManagerObjectProp, new GUIContent("目标管理器对象"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    
                    // 当管理器对象改变时，自动检测管理器类型
                    GameObject targetObj = targetManagerObjectProp.objectReferenceValue as GameObject;
                    if (targetObj != null)
                    {
                        // 获取所有MonoBehaviour组件
                        MonoBehaviour[] components = targetObj.GetComponents<MonoBehaviour>();
                        if (components.Length > 0)
                        {
                            // 使用第一个组件作为管理器类型
                            Type managerType = components[0].GetType();
                            managerTypeNameProp.stringValue = managerType.FullName;
                            
                            // 清除字段引用列表
                            fieldReferencesProp.ClearArray();
                            serializedObject.ApplyModifiedProperties();
                            
                            // 自动添加所有可用字段
                            AutoAddAvailableFields(managerType, fieldReferencesProp);
                        }
                    }
                    else
                    {
                        managerTypeNameProp.stringValue = "";
                        serializedObject.ApplyModifiedProperties();
                    }
                }
                
                // 管理器类型信息
                if (!string.IsNullOrEmpty(managerTypeNameProp.stringValue))
                {
                    EditorGUILayout.LabelField("管理器类型", managerTypeNameProp.stringValue);
                    
                    // 字段引用列表
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("字段引用", EditorStyles.boldLabel);
                    
                    // 添加字段按钮
                    if (GUILayout.Button("添加可用字段"))
                    {
                        AddAvailableFields(managerTypeNameProp.stringValue, fieldReferencesProp);
                    }
                    
                    // 绘制字段引用列表
                    for (int j = 0; j < fieldReferencesProp.arraySize; j++)
                    {
                        SerializedProperty fieldRefProp = fieldReferencesProp.GetArrayElementAtIndex(j);
                        SerializedProperty fieldNameProp = fieldRefProp.FindPropertyRelative("fieldName");
                        SerializedProperty sceneObjectProp = fieldRefProp.FindPropertyRelative("sceneObject");
                        SerializedProperty fieldTypeNameProp = fieldRefProp.FindPropertyRelative("fieldTypeName");
                        SerializedProperty isComponentTypeProp = fieldRefProp.FindPropertyRelative("isComponentType");
                        SerializedProperty componentReferenceProp = fieldRefProp.FindPropertyRelative("componentReference");
                        
                        string fieldKey = $"{managerKey}_field_{j}";
                        if (!fieldFoldoutStates.ContainsKey(fieldKey))
                        {
                            fieldFoldoutStates[fieldKey] = true;
                        }
                        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        // 字段标题行
                        EditorGUILayout.BeginHorizontal();
                        fieldFoldoutStates[fieldKey] = EditorGUILayout.Foldout(fieldFoldoutStates[fieldKey], 
                            $"字段: {fieldNameProp.stringValue}");
                        
                        // 删除字段按钮
                        if (GUILayout.Button("删除", GUILayout.Width(60)))
                        {
                            fieldReferencesProp.DeleteArrayElementAtIndex(j);
                            serializedObject.ApplyModifiedProperties();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                        
                        if (fieldFoldoutStates[fieldKey])
                        {
                            EditorGUI.indentLevel++;
                            
                            // 显示字段类型
                            EditorGUILayout.LabelField("字段类型", fieldTypeNameProp.stringValue);
                            
                            // 场景对象引用
                            EditorGUILayout.PropertyField(sceneObjectProp, new GUIContent("场景对象"));
                            
                            // 如果字段类型是组件类型，显示组件选择下拉菜单
                            if (isComponentTypeProp.boolValue)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("组件引用", GUILayout.Width(120));
                                
                                // 获取可用的组件列表
                                GameObject selectedObj = sceneObjectProp.objectReferenceValue as GameObject;
                                if (selectedObj != null)
                                {
                                    // 获取字段类型
                                    Type fieldType = GetTypeFromName(fieldTypeNameProp.stringValue);
                                    if (fieldType != null && typeof(Component).IsAssignableFrom(fieldType))
                                    {
                                        // 获取所有匹配类型的组件
                                        Component[] components = selectedObj.GetComponents(fieldType);
                                        string[] componentNames = new string[components.Length + 1];
                                        componentNames[0] = "无";
                                        
                                        for (int k = 0; k < components.Length; k++)
                                        {
                                            componentNames[k + 1] = components[k].GetType().Name;
                                        }
                                        
                                        // 找到当前选择的组件索引
                                        int currentIndex = 0;
                                        Component currentComponent = componentReferenceProp.objectReferenceValue as Component;
                                        if (currentComponent != null)
                                        {
                                            for (int k = 0; k < components.Length; k++)
                                            {
                                                if (components[k] == currentComponent)
                                                {
                                                    currentIndex = k + 1;
                                                    break;
                                                }
                                            }
                                        }
                                        
                                        // 显示下拉菜单
                                        int newIndex = EditorGUILayout.Popup(currentIndex, componentNames);
                                        if (newIndex != currentIndex)
                                        {
                                            if (newIndex == 0)
                                            {
                                                componentReferenceProp.objectReferenceValue = null;
                                            }
                                            else
                                            {
                                                componentReferenceProp.objectReferenceValue = components[newIndex - 1];
                                            }
                                            serializedObject.ApplyModifiedProperties();
                                        }
                                    }
                                    else
                                    {
                                        EditorGUILayout.LabelField("不是组件类型");
                                    }
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("请先选择场景对象");
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            
                            EditorGUI.indentLevel--;
                        }
                        
                        EditorGUILayout.EndVertical();
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        // 添加管理器按钮
        if (GUILayout.Button("添加管理器引用"))
        {
            AddManagerReference();
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    // 添加管理器引用
    private void AddManagerReference()
    {
        int index = managerReferencesProperty.arraySize;
        managerReferencesProperty.arraySize++;
        SerializedProperty newManagerRef = managerReferencesProperty.GetArrayElementAtIndex(index);
        newManagerRef.FindPropertyRelative("targetManagerObject").objectReferenceValue = null;
        newManagerRef.FindPropertyRelative("managerTypeName").stringValue = "";
        newManagerRef.FindPropertyRelative("fieldReferences").ClearArray();
        serializedObject.ApplyModifiedProperties();
    }
    
    // 添加可用字段
    private void AddAvailableFields(string managerTypeName, SerializedProperty fieldReferencesProp)
    {
        Debug.Log($"尝试添加字段，管理器类型: {managerTypeName}");
        
        Type managerType = GetTypeFromName(managerTypeName);
        if (managerType == null)
        {
            string message = $"无法找到类型: {managerTypeName}";
            Debug.LogWarning(message);
            EditorUtility.DisplayDialog("错误", message, "确定");
            return;
        }
        
        Debug.Log($"成功找到类型: {managerType.FullName}");
        int addedCount = AutoAddAvailableFields(managerType, fieldReferencesProp);
        
        serializedObject.ApplyModifiedProperties();
        
        if (addedCount == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在 {managerType.Name} 中没有找到可用的GameObject或Component类型字段", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("成功", $"已添加 {addedCount} 个可用字段", "确定");
        }
    }
    
    // 自动添加所有可用字段
    private int AutoAddAvailableFields(Type managerType, SerializedProperty fieldReferencesProp)
{
    int addedCount = 0;
    
    try
    {
        // 获取所有字段（包括私有字段）
        FieldInfo[] allFields = managerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        List<FieldInfo> validFields = new List<FieldInfo>();
        
        // 筛选公共字段和带有SerializeField特性的私有字段
        foreach (var field in allFields)
        {
            if (field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), false).Length > 0)
            {
                validFields.Add(field);
            }
        }
        
        Debug.Log($"找到 {validFields.Count} 个有效字段");
        
        // 处理有效字段
        foreach (var field in validFields)
        {
            // 检查字段类型是否为GameObject或Component
            Type fieldType = field.FieldType;
            bool isGameObjectType = fieldType == typeof(GameObject);
            bool isComponentType = typeof(Component).IsAssignableFrom(fieldType);
            
            Debug.Log($"字段: {field.Name}, 类型: {fieldType.Name}, GameObject类型: {isGameObjectType}, Component类型: {isComponentType}");
            
            if (isGameObjectType || isComponentType)
            {
                // 检查是否已存在该字段
                bool exists = false;
                for (int i = 0; i < fieldReferencesProp.arraySize; i++)
                {
                    SerializedProperty existingField = fieldReferencesProp.GetArrayElementAtIndex(i);
                    if (existingField.FindPropertyRelative("fieldName").stringValue == field.Name)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    int index = fieldReferencesProp.arraySize;
                    fieldReferencesProp.arraySize++;
                    SerializedProperty newField = fieldReferencesProp.GetArrayElementAtIndex(index);
                    
                    newField.FindPropertyRelative("fieldName").stringValue = field.Name;
                    newField.FindPropertyRelative("fieldTypeName").stringValue = fieldType.Name;
                    newField.FindPropertyRelative("isProperty").boolValue = false;
                    newField.FindPropertyRelative("isComponentType").boolValue = isComponentType;
                    
                    addedCount++;
                }
            }
        }
        
        // 处理属性（保持原有代码）
        PropertyInfo[] properties = managerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        // ...处理属性的代码保持不变...
    }
    catch (Exception e)
    {
        Debug.LogError($"添加可用字段时出错: {e.Message}\n{e.StackTrace}");
    }
    
    return addedCount;
}

    
    // 通过名称获取类型
    private Type GetTypeFromName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;
            
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }
        
        // 如果找不到完全匹配的类型名，尝试部分匹配
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.FullName != null && type.FullName.EndsWith(typeName))
                    return type;
            }
        }
        
        return null;
    }
}
#endif
