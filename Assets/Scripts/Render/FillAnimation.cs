using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在保存图标Prefab上，负责循环填充动画
/// </summary>
public class SaveIconLoadingAnimation : MonoBehaviour
{
    public Image ringImage;
    public float speed = 1.5f; // 一圈所需秒数

    void Update()
    {
        if (ringImage != null && gameObject.activeInHierarchy)
        {
            ringImage.fillAmount = (Time.time * speed) % 1.0f;
        }
    }
}