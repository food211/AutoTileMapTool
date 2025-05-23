using UnityEngine;
using UnityEditor;

public class HierarchyParentWrapper : EditorWindow
{
  [MenuItem("Tools/Wrap Selected Objects with Parent")]
  public static void WrapSelectedObjects()
  {
      // 获取当前选中的所有GameObject
      GameObject[] selectedObjects = Selection.gameObjects;
      
      if (selectedObjects.Length == 0)
      {
          Debug.LogWarning("请先选择要处理的GameObject");
          return;
      }
      
      // 记录操作用于Undo
      Undo.RegisterCompleteObjectUndo(selectedObjects, "Wrap Objects with Parent");
      
      foreach (GameObject selectedObj in selectedObjects)
      {
          WrapWithParent(selectedObj);
      }
      
      Debug.Log($"成功为 {selectedObjects.Length} 个对象创建了父空物体");
  }
  
  private static void WrapWithParent(GameObject targetObject)
  {
      // 记录原始的Transform信息
      Vector3 originalPosition = targetObject.transform.position;
      Quaternion originalRotation = targetObject.transform.rotation;
      Vector3 originalScale = targetObject.transform.localScale;
      Transform originalParent = targetObject.transform.parent;
      int originalSiblingIndex = targetObject.transform.GetSiblingIndex();
      
      // 创建空的父对象
      GameObject parentObject = new GameObject(targetObject.name + "_Parent");
      
      // 注册父对象的创建操作用于Undo
      Undo.RegisterCreatedObjectUndo(parentObject, "Create Parent Object");
      
      // 设置父对象的Transform为目标对象的Transform
      parentObject.transform.position = originalPosition;
      parentObject.transform.rotation = originalRotation;
      parentObject.transform.localScale = originalScale;
      
      // 将父对象放在原来目标对象的位置（在hierarchy中）
      parentObject.transform.SetParent(originalParent);
      parentObject.transform.SetSiblingIndex(originalSiblingIndex);
      
      // 将目标对象设为父对象的子物体
      Undo.SetTransformParent(targetObject.transform, parentObject.transform, "Set Parent");
      
      // 重置目标对象的本地坐标
      targetObject.transform.localPosition = Vector3.zero;
      targetObject.transform.localRotation = Quaternion.identity;
      targetObject.transform.localScale = Vector3.one;
  }
  
  // 验证菜单项是否可用
  [MenuItem("Tools/Wrap Selected Objects with Parent", true)]
  public static bool ValidateWrapSelectedObjects()
  {
      return Selection.gameObjects.Length > 0;
  }
}

// 右键菜单版本
public class HierarchyParentWrapperContext : Editor
{
  [MenuItem("GameObject/Wrap with Parent", false, 0)]
  public static void WrapWithParentContext()
  {
      HierarchyParentWrapper.WrapSelectedObjects();
  }
  
  [MenuItem("GameObject/Wrap with Parent", true)]
  public static bool ValidateWrapWithParentContext()
  {
      return Selection.gameObjects.Length > 0;
  }
}