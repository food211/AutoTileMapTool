using UnityEngine;

/// <summary>
/// 使物体在运行时自动隐藏，但在编辑器中保持显示
/// </summary>
public class HideOnPlay : MonoBehaviour
{
    private void Awake()
    {
        // 在游戏开始时立即隐藏物体
        gameObject.SetActive(false);
    }

    // 在编辑器中，物体会保持显示状态
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 这个函数在编辑器中会被调用，但不会影响运行时行为
        // 它的存在只是为了确保在编辑器中可以看到这个组件
    }
    #endif
}