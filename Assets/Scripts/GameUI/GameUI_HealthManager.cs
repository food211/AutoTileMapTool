using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理玩家生命值UI显示的脚本
/// </summary>
public class GameUI_HealthManager : MonoBehaviour
{
    [Header("生命值UI设置")]
    [SerializeField] private Transform heartContainer; // 存放心形图标的容器
    [SerializeField] private GameObject heartPrefab; // 心形图标预制体
    
    [Header("心形图标精灵")]
    [SerializeField] private Sprite normalHeartSprite; // 正常心形图标 (GameUI_gameui_heart)
    [SerializeField] private Sprite emptyHeartSprite; // 空心形图标 (GameUI_gameui_noheart)
    [SerializeField] private Sprite shieldHeartSprite; // 护盾心形图标 (GameUI_gameui_sheildheart)
    [SerializeField] private Sprite disabledHeartSprite; // 禁用心形图标 (GameUI_gameui_x)
    
    [Header("动画设置")]
    [SerializeField] private float heartBeatScale = 1.2f; // 心跳动画缩放比例
    [SerializeField] private float heartBeatDuration = 0.3f; // 心跳动画持续时间
    
    // 内部变量
    private List<Image> heartImages = new List<Image>(); // 所有心形图标的Image组件
    private int maxHealth = 5; // 最大生命值
    private int currentHealth = 5; // 当前生命值
    private int maxDisplayHealth = 5; // 最大显示生命值（可能受限制）
    private int shieldHealth = 0; // 护盾生命值
    
    private void OnEnable()
    {
        // 订阅玩家生命值变化事件
        GameEvents.OnPlayerHealthChanged += UpdateHealthUI;
        
        // 可以根据需要订阅其他事件，如玩家重生等
        GameEvents.OnPlayerRespawnCompleted += OnPlayerRespawned;
    }
    
    private void OnDisable()
    {
        // 取消订阅事件
        GameEvents.OnPlayerHealthChanged -= UpdateHealthUI;
        GameEvents.OnPlayerRespawnCompleted -= OnPlayerRespawned;
    }
    
    private void Start()
    {
        InitializeHearts();
    }
    
    /// <summary>
    /// 初始化心形图标
    /// </summary>
    private void InitializeHearts()
    {
        // 清空现有的心形图标
        ClearHearts();
        
        // 创建心形图标
        for (int i = 0; i < maxHealth; i++)
        {
            CreateHeartIcon();
        }
        
        // 更新UI显示
        UpdateHealthDisplay();
    }
    
    /// <summary>
    /// 清空所有心形图标
    /// </summary>
    private void ClearHearts()
    {
        // 销毁所有子物体
        foreach (Transform child in heartContainer)
        {
            Destroy(child.gameObject);
        }
        
        // 清空列表
        heartImages.Clear();
    }
    
    /// <summary>
    /// 创建单个心形图标
    /// </summary>
    private void CreateHeartIcon()
    {
        // 实例化心形图标预制体
        GameObject heartObj = Instantiate(heartPrefab, heartContainer);
        
        // 获取Image组件
        Image heartImage = heartObj.GetComponent<Image>();
        if (heartImage != null)
        {
            // 设置初始图标为正常心形
            heartImage.sprite = normalHeartSprite;
            
            // 添加到列表
            heartImages.Add(heartImage);
        }
    }
    
    /// <summary>
    /// 更新生命值UI
    /// </summary>
    private void UpdateHealthUI(int newHealth, int newMaxHealth)
    {
        // 更新内部变量
        currentHealth = newHealth;
        
        // 如果最大生命值发生变化
        if (maxHealth != newMaxHealth)
        {
            maxHealth = newMaxHealth;
            
            // 如果最大显示生命值未被限制，则同步更新
            if (maxDisplayHealth == maxHealth || maxDisplayHealth < newMaxHealth)
            {
                maxDisplayHealth = maxHealth;
            }
            
            // 重新初始化心形图标
            InitializeHearts();
        }
        else
        {
            // 仅更新显示
            UpdateHealthDisplay();
            
            // 如果生命值减少，播放受伤动画
            if (newHealth < currentHealth)
            {
                PlayDamageAnimation();
            }
        }
    }
    
    /// <summary>
    /// 更新心形图标显示
    /// </summary>
    private void UpdateHealthDisplay()
    {
        // 确保有足够的心形图标
        while (heartImages.Count < maxHealth)
        {
            CreateHeartIcon();
        }
        
        // 更新每个心形图标
        for (int i = 0; i < heartImages.Count; i++)
        {
            if (i < maxDisplayHealth) // 在显示限制范围内
            {
                heartImages[i].gameObject.SetActive(true);
                
                if (i < currentHealth) // 有生命值
                {
                    if (i >= currentHealth - shieldHealth) // 护盾生命值
                    {
                        heartImages[i].sprite = shieldHeartSprite;
                    }
                    else // 正常生命值
                    {
                        heartImages[i].sprite = normalHeartSprite;
                    }
                }
                else // 空生命值
                {
                    heartImages[i].sprite = emptyHeartSprite;
                }
            }
            else if (i < maxHealth) // 超出显示限制但在最大生命值范围内
            {
                heartImages[i].gameObject.SetActive(true);
                heartImages[i].sprite = disabledHeartSprite; // 显示为禁用状态
            }
            else // 超出最大生命值
            {
                heartImages[i].gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 播放受伤动画
    /// </summary>
    private void PlayDamageAnimation()
    {
        // 停止所有正在运行的动画协程
        StopAllCoroutines();
        
        // 开始新的动画协程
        StartCoroutine(HeartBeatAnimation());
    }
    
    /// <summary>
    /// 心跳动画协程
    /// </summary>
    private IEnumerator HeartBeatAnimation()
    {
        // 获取最后一个有效心形图标的索引
        int lastHeartIndex = Mathf.Min(currentHealth, heartImages.Count) - 1;
        
        // 如果没有有效的心形图标，直接返回
        if (lastHeartIndex < 0)
            yield break;
            
        // 获取目标心形图标
        Image targetHeart = heartImages[lastHeartIndex];
        Transform heartTransform = targetHeart.transform;
        
        // 保存原始大小
        Vector3 originalScale = heartTransform.localScale;
        
        // 放大动画
        float elapsed = 0f;
        while (elapsed < heartBeatDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (heartBeatDuration / 2);
            heartTransform.localScale = Vector3.Lerp(originalScale, originalScale * heartBeatScale, t);
            yield return null;
        }
        
        // 缩小动画
        elapsed = 0f;
        while (elapsed < heartBeatDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (heartBeatDuration / 2);
            heartTransform.localScale = Vector3.Lerp(originalScale * heartBeatScale, originalScale, t);
            yield return null;
        }
        
        // 确保恢复原始大小
        heartTransform.localScale = originalScale;
    }
    
    /// <summary>
    /// 设置护盾生命值
    /// </summary>
    /// <param name="amount">护盾数量</param>
    public void SetShieldHealth(int amount)
    {
        shieldHealth = Mathf.Max(0, amount);
        UpdateHealthDisplay();
    }
    
    /// <summary>
    /// 设置最大显示生命值（用于临时限制生命值上限）
    /// </summary>
    /// <param name="limit">显示上限</param>
    public void SetMaxDisplayHealth(int limit)
    {
        maxDisplayHealth = Mathf.Clamp(limit, 0, maxHealth);
        UpdateHealthDisplay();
    }
    
    /// <summary>
    /// 重置最大显示生命值
    /// </summary>
    public void ResetMaxDisplayHealth()
    {
        maxDisplayHealth = maxHealth;
        UpdateHealthDisplay();
    }
    
    /// <summary>
    /// 玩家重生后的处理
    /// </summary>
    private void OnPlayerRespawned()
    {
        // 重置护盾
        shieldHealth = 0;
        
        // 重置显示限制
        ResetMaxDisplayHealth();
        
        // 更新显示
        UpdateHealthDisplay();
    }
}