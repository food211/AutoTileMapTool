using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VelocityDisplay : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Rigidbody2D targetRigidbody; // 要监视的刚体
    [SerializeField] private PlayerController playerController; // 玩家控制器
    [SerializeField] private RopeSystem ropeSystem; // 绳索系统
    
    [Header("UI设置")]
    [SerializeField] private Canvas displayCanvas; // 显示用的Canvas
    [SerializeField] private TextMeshProUGUI velocityText; // 速度文本
    [SerializeField] private TextMeshProUGUI stateText; // 状态文本
    
    [Header("显示设置")]
    [SerializeField] private bool showVelocity = true; // 是否显示速度
    [SerializeField] private bool showState = true; // 是否显示状态
    [SerializeField] private Color normalColor = Color.white; // 普通颜色
    [SerializeField] private Color highSpeedColor = Color.green; // 高速颜色
    [SerializeField] private float highSpeedThreshold = 15f; // 高速阈值
    
    // 如果没有在Inspector中指定，在Start中自动查找组件
    private void Start()
    {
        if (targetRigidbody == null)
        {
            // 尝试在同一对象上查找Rigidbody2D
            targetRigidbody = GetComponent<Rigidbody2D>();
            
            // 如果还是没有找到，尝试查找PlayerController并获取其Rigidbody2D
            if (targetRigidbody == null && playerController != null)
            {
                targetRigidbody = playerController.GetComponent<Rigidbody2D>();
            }
            
            // 如果还是没有找到，在场景中查找PlayerController
            if (targetRigidbody == null)
            {
                PlayerController foundController = FindObjectOfType<PlayerController>();
                if (foundController != null)
                {
                    playerController = foundController;
                    targetRigidbody = foundController.GetComponent<Rigidbody2D>();
                }
            }
        }
        
        if (playerController == null && targetRigidbody != null)
        {
            playerController = targetRigidbody.GetComponent<PlayerController>();
        }
        
        if (ropeSystem == null)
        {
            ropeSystem = FindObjectOfType<RopeSystem>();
        }
        
        // 如果没有指定Canvas，创建一个
        if (displayCanvas == null)
        {
            // 查找现有的Canvas
            displayCanvas = FindObjectOfType<Canvas>();
            
            // 如果场景中没有Canvas，创建一个
            if (displayCanvas == null)
            {
                GameObject canvasObj = new GameObject("VelocityDisplayCanvas");
                displayCanvas = canvasObj.AddComponent<Canvas>();
                displayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }
        
        // 如果没有指定文本组件，创建一个
        if (velocityText == null)
        {
            GameObject textObj = new GameObject("VelocityText");
            textObj.transform.SetParent(displayCanvas.transform, false);
            velocityText = textObj.AddComponent<TextMeshProUGUI>();
            velocityText.fontSize = 20;
            velocityText.alignment = TextAlignmentOptions.TopLeft;
            velocityText.rectTransform.anchoredPosition = new Vector2(10, -10);
            velocityText.rectTransform.sizeDelta = new Vector2(300, 100);
            velocityText.rectTransform.anchorMin = new Vector2(0, 1);
            velocityText.rectTransform.anchorMax = new Vector2(0, 1);
        }
        
        // 如果没有指定状态文本组件，创建一个
        if (stateText == null)
        {
            GameObject textObj = new GameObject("StateText");
            textObj.transform.SetParent(displayCanvas.transform, false);
            stateText = textObj.AddComponent<TextMeshProUGUI>();
            stateText.fontSize = 20;
            stateText.alignment = TextAlignmentOptions.TopLeft;
            stateText.rectTransform.anchoredPosition = new Vector2(10, -50);
            stateText.rectTransform.sizeDelta = new Vector2(300, 100);
            stateText.rectTransform.anchorMin = new Vector2(0, 1);
            stateText.rectTransform.anchorMax = new Vector2(0, 1);
        }
        
        // 初始化时根据showVelocity和showState设置组件可见性
        UpdateComponentVisibility();
    }
    
    private void Update()
    {
        if (targetRigidbody == null) return;

        UpdateComponentVisibility();
        
        // 获取速度信息
        Vector2 velocity = targetRigidbody.velocity;
        float speed = velocity.magnitude;
        
        // 更新速度文本
        if (showVelocity && velocityText != null)
        {
            // 设置颜色
            velocityText.color = speed > highSpeedThreshold ? highSpeedColor : normalColor;
            
            // 格式化速度信息
            velocityText.text = string.Format(
                "Velocity: {0:F2} m/s\n" +
                "X: {1:F2} m/s\n" +
                "Y: {2:F2} m/s",
                speed,
                velocity.x,
                velocity.y
            );
        }
        
        // 更新状态文本
        if (showState && stateText != null)
        {
            string state = "Unkown";
            
            if (playerController != null)
            {
                if (playerController.isPlayerGrounded())
                {
                    state = "Grounded";
                }
                else if (playerController.isPlayerRopeMode())
                {
                    state = "RopeMode";
                }
                else
                {
                    state = "inAir";
                }
            }
            
            // 如果有绳索系统，显示绳索状态
            if (ropeSystem != null)
            {
                if (ropeSystem.IsShooting())
                {
                    state += " (RopeShooting)";
                }
                else if (ropeSystem.IsRopeShootingOrHooked() && !ropeSystem.IsShooting())
                {
                    state += " (Hooked)";
                    
                    // 显示当前钩中的物体标签
                    string hookTag = ropeSystem.GetCurrentHookTag();
                    if (!string.IsNullOrEmpty(hookTag))
                    {
                        state += " - " + hookTag;
                    }
                }
            }
            
            stateText.text = "State: " + state;
        }
    }
    
    // 更新组件可见性
    private void UpdateComponentVisibility()
    {
        if (velocityText != null)
            velocityText.gameObject.SetActive(showVelocity);

        if (stateText != null)
            stateText.gameObject.SetActive(showState);
    }
}