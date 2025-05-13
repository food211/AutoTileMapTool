using UnityEditor;
using UnityEngine;

public class Locker
{
    [MenuItem("GameObject/LockTools/Lock",false,0)]
    static void Lock()
    {
        if (Selection.gameObjects != null){
            foreach (var gameObject in Selection.gameObjects)
            {
                gameObject.hideFlags = HideFlags.NotEditable;
            }
        }
    }
    [MenuItem("GameObject/LockTools/UnLock",false,1)]
    static void UnLock()
    {
        if (Selection.gameObjects != null){
            foreach (var gameObject in Selection.gameObjects)
            {
                gameObject.hideFlags = HideFlags.None;
            }
        }
    }
}