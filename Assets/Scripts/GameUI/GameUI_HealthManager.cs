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
    [SerializeField] private RectTransform heartContainer; // 使用RectTransform而不是Transform
    [SerializeField] private GameObject heartPrefab; // 心形图标预制体
    [SerializeField] private float heartSpacing = 10f; // 心形图标之间的间距
    
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
    bool debugmode = false;
    
private void OnEnable()
    {
        // 订阅玩家生命值变化事件
        GameEvents.OnPlayerHealthChanged += UpdateHealthUI;
        // 订阅玩家护盾变化事件
        GameEvents.OnPlayerShieldChanged += UpdateShieldUI;
        // 订阅最大生命值限制变化事件
        GameEvents.OnPlayerMaxHealthLimitChanged += UpdateMaxHealthLimit;
        // 可以根据需要订阅其他事件，如玩家重生等
        GameEvents.OnPlayerRespawnCompleted += OnPlayerRespawned;
    }
    
private void OnDisable()
{
    // 取消订阅事件
    GameEvents.OnPlayerHealthChanged -= UpdateHealthUI;
    GameEvents.OnPlayerShieldChanged -= UpdateShieldUI;
    GameEvents.OnPlayerMaxHealthLimitChanged -= UpdateMaxHealthLimit;
    GameEvents.OnPlayerRespawnCompleted -= OnPlayerRespawned;
}

    private void Start()
    {
        // 检查并获取当前玩家的生命值信息
        PlayerHealthManager playerHealth = FindObjectOfType<PlayerHealthManager>();
        if (playerHealth != null)
        {
            maxHealth = playerHealth.GetMaxHealth();
            currentHealth = playerHealth.GetCurrentHealth();
            shieldHealth = playerHealth.GetCurrentShield(); // 获取当前护盾值
        }

        InitializeHearts();
    }
    
    /// <summary>
    /// 初始化心形图标
    /// </summary>
    private void InitializeHearts()
    {
        // 清空现有的心形图标
        ClearHearts();
        
        // 确保心形容器存在
        if (heartContainer == null)
        {
            Debug.LogError("心形容器未设置！请在Inspector中设置心形容器。");
            return;
        }
        
        // 确保心形预制体存在
        if (heartPrefab == null)
        {
            Debug.LogError("心形预制体未设置！请在Inspector中设置心形预制体。");
            return;
        }
        
        // 创建心形图标
        for (int i = 0; i < maxHealth; i++)
        {
            CreateHeartIcon(i);
        }
        
        // 更新UI显示
        UpdateHealthDisplay();
    }

    /// <summary>
    /// 更新最大生命值限制
    /// </summary>
    private void UpdateMaxHealthLimit(int newMaxHealthLimit, int realMaxHealth)
    {
        maxDisplayHealth = newMaxHealthLimit;
        UpdateHealthDisplay();

        // 调试信息
        if (debugmode)
        Debug.Log($"UI更新最大生命值限制: {maxDisplayHealth} / {maxHealth}");
    }

    private void UpdateShieldUI(int newShield, int newMaxShield)
    {
        // 保存旧的护盾值用于比较
        int oldShield = shieldHealth;

        // 更新护盾值
        shieldHealth = newShield;

        // 更新显示
        UpdateHealthDisplay();

        // 如果护盾值减少，播放护盾损失动画
        if (newShield < oldShield)
        {
            PlayShieldLossAnimation(newShield, oldShield);
        }
        // 如果护盾值增加，播放获得护盾动画
        else if (newShield > oldShield)
        {
            PlayShieldGainAnimation(oldShield, newShield);
        }

#if UNITY_EDITOR
        // 调试信息
        if (debugmode)
            Debug.Log($"护盾更新: {oldShield} -> {shieldHealth} / {newMaxShield}");
#endif
    }

    /// <summary>
    /// 播放护盾损失动画
    /// </summary>
    private void PlayShieldLossAnimation(int newShield, int oldShield)
    {
        // 停止所有正在运行的动画协程
        StopAllCoroutines();

        // 开始新的动画协程 - 按顺序闪烁所有受影响的心形
        StartCoroutine(SequentialShieldLossAnimation(newShield, oldShield));
    }

    /// <summary>
    /// 顺序护盾损失动画协程
    /// </summary>
    private IEnumerator SequentialShieldLossAnimation(int newShield, int oldShield)
    {
        // 计算受影响的心形索引范围
        int startIndex = currentHealth - oldShield; // 原护盾开始位置
        int endIndex = currentHealth - newShield - 1; // 原护盾结束位置

        // 从右到左闪烁失去的护盾心形
        for (int i = endIndex; i >= startIndex && i >= 0 && i < heartImages.Count; i--)
        {
            if (heartImages[i] == null) continue;

            // 对当前心形执行闪烁动画
            yield return StartCoroutine(SingleShieldLossAnimation(heartImages[i]));

            // 在每个心形动画之间添加小延迟
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>
    /// 单个心形的护盾损失动画
    /// </summary>
    private IEnumerator SingleShieldLossAnimation(Image heartImage)
    {
        if (heartImage == null) yield break;

        Transform heartTransform = heartImage.transform;

        // 保存原始大小和颜色
        Vector3 originalScale = heartTransform.localScale;
        Color originalColor = heartImage.color;

        // 设置为蓝色
        heartImage.color = new Color(0.3f, 0.6f, 1f); // 蓝色

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

        // 恢复原始大小和颜色
        heartTransform.localScale = originalScale;
        heartImage.color = originalColor;
    }

    /// <summary>
    /// 播放获得护盾动画
    /// </summary>
    private void PlayShieldGainAnimation(int oldShield, int newShield)
    {
        // 停止所有正在运行的动画协程
        StopAllCoroutines();

        // 开始新的动画协程 - 按顺序闪烁所有受影响的心形
        StartCoroutine(SequentialShieldGainAnimation(oldShield, newShield));
    }

    /// <summary>
    /// 顺序获得护盾动画协程
    /// </summary>
    private IEnumerator SequentialShieldGainAnimation(int oldShield, int newShield)
    {
        // 计算受影响的心形索引范围
        int startIndex = currentHealth - oldShield - 1; // 新护盾开始位置
        int endIndex = currentHealth - newShield; // 新护盾结束位置

        // 从左到右闪烁获得的护盾心形
        for (int i = endIndex; i <= startIndex && i >= 0 && i < heartImages.Count; i++)
        {
            if (heartImages[i] == null) continue;

            // 对当前心形执行闪烁动画
            yield return StartCoroutine(SingleShieldGainAnimation(heartImages[i]));

            // 在每个心形动画之间添加小延迟
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>
    /// 单个心形的获得护盾动画
    /// </summary>
    private IEnumerator SingleShieldGainAnimation(Image heartImage)
    {
        if (heartImage == null) yield break;

        Transform heartTransform = heartImage.transform;

        // 保存原始大小和颜色
        Vector3 originalScale = heartTransform.localScale;
        Color originalColor = heartImage.color;

        // 设置为蓝色
        heartImage.color = new Color(0.3f, 0.6f, 1f); // 蓝色

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

        // 恢复原始大小和颜色
        heartTransform.localScale = originalScale;
        heartImage.color = originalColor;
    }

    /// <summary>
    /// 清空所有心形图标
    /// </summary>
    private void ClearHearts()
    {
        // 销毁所有子物体
        if (heartContainer != null)
        {
            foreach (Transform child in heartContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 清空列表
        heartImages.Clear();
    }
    
    /// <summary>
    /// 创建单个心形图标
    /// </summary>
    private void CreateHeartIcon(int index)
    {
        // 实例化心形图标预制体
        GameObject heartObj = Instantiate(heartPrefab, heartContainer);
        
        // 设置名称
        heartObj.name = "Heart_" + index;
        
        // 获取RectTransform组件
        RectTransform heartRect = heartObj.GetComponent<RectTransform>();
        if (heartRect != null)
        {
            // 设置位置 - 根据索引水平排列
            float xPos = index * (heartRect.rect.width + heartSpacing);
            heartRect.anchoredPosition = new Vector2(xPos, 0);
        }
        
        // 获取Image组件
        Image heartImage = heartObj.GetComponent<Image>();
        if (heartImage == null)
        {
            // 如果没有Image组件，添加一个
            heartImage = heartObj.AddComponent<Image>();
        }
        
        // 设置初始图标为正常心形
        if (normalHeartSprite != null)
        {
            heartImage.sprite = normalHeartSprite;
        }
        else
        {
            Debug.LogWarning("正常心形精灵未设置！请在Inspector中设置正常心形精灵。");
        }
        
        // 添加到列表
        heartImages.Add(heartImage);
    }

    /// <summary>
    /// 更新生命值UI
    /// </summary>
    private void UpdateHealthUI(int newHealth, int newMaxHealth)
    {
        // 保存旧的生命值用于比较
        int oldHealth = currentHealth;

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
            if (newHealth < oldHealth)
            {
                PlayDamageAnimation();
            }
            // 如果生命值增加，播放治疗动画
            else if (newHealth > oldHealth)
            {
                PlayHealAnimation(oldHealth, newHealth);
            }
        }
#if UNITY_EDITOR
        // 调试信息
        if (debugmode)
            Debug.Log($"生命值更新: {oldHealth} -> {newHealth} / {maxHealth}");
#endif  
    }

    /// <summary>
    /// 播放治疗动画
    /// </summary>
    private void PlayHealAnimation(int oldHealth, int newHealth)
    {
        // 停止所有正在运行的动画协程
        StopAllCoroutines();

        // 开始新的动画协程 - 按顺序闪烁所有受影响的心形
        StartCoroutine(SequentialHealAnimation(oldHealth, newHealth));
    }

    /// <summary>
    /// 顺序治疗动画协程
    /// </summary>
    private IEnumerator SequentialHealAnimation(int oldHealth, int newHealth)
    {
        // 从左到右（从最低索引到最高索引）闪烁新增加的心形
        for (int i = oldHealth; i < newHealth && i < heartImages.Count; i++)
        {
            if (heartImages[i] == null) continue;

            // 对当前心形执行闪烁动画
            yield return StartCoroutine(SingleHeartHealAnimation(heartImages[i]));

            // 在每个心形动画之间添加小延迟
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>
    /// 单个心形的治疗动画
    /// </summary>
    private IEnumerator SingleHeartHealAnimation(Image heartImage)
    {
        if (heartImage == null) yield break;

        Transform heartTransform = heartImage.transform;

        // 保存原始大小和颜色
        Vector3 originalScale = heartTransform.localScale;
        Color originalColor = heartImage.color;

        // 设置为绿色
        heartImage.color = Color.green;

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

        // 恢复原始大小和颜色
        heartTransform.localScale = originalScale;
        heartImage.color = originalColor;
    }

    /// <summary>
    /// 更新心形图标显示
    /// </summary>
    private void UpdateHealthDisplay()
    {
        // 确保有足够的心形图标
        while (heartImages.Count < maxHealth)
        {
            CreateHeartIcon(heartImages.Count);
        }

        // 更新每个心形图标
        for (int i = 0; i < heartImages.Count; i++)
        {
            if (heartImages[i] == null) continue;

            if (i < maxDisplayHealth) // 在显示限制范围内
            {
                heartImages[i].gameObject.SetActive(true);

                if (i < currentHealth) // 有生命值
                {
                    if (i >= currentHealth - shieldHealth && shieldHeartSprite != null) // 护盾生命值
                    {
                        heartImages[i].sprite = shieldHeartSprite;
                    }
                    else if (normalHeartSprite != null) // 正常生命值
                    {
                        heartImages[i].sprite = normalHeartSprite;
                    }
                }
                else if (emptyHeartSprite != null) // 空生命值
                {
                    heartImages[i].sprite = emptyHeartSprite;
                }
            }
            else if (i < maxHealth && disabledHeartSprite != null) // 超出显示限制但在最大生命值范围内
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

        // 开始新的动画协程 - 按顺序闪烁所有受影响的心形
        StartCoroutine(SequentialHeartBeatAnimation());
    }

    /// <summary>
    /// 顺序心跳动画协程 - Minecraft风格
    /// </summary>
    private IEnumerator SequentialHeartBeatAnimation()
    {
        // 从右到左（从最高索引到最低索引）闪烁心形
        for (int i = currentHealth; i < heartImages.Count && i < maxDisplayHealth; i++)
        {
            if (heartImages[i] == null) continue;

            // 对当前心形执行闪烁动画
            yield return StartCoroutine(SingleHeartBeatAnimation(heartImages[i]));

            // 在每个心形动画之间添加小延迟
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>
    /// 单个心形的心跳动画
    /// </summary>
    private IEnumerator SingleHeartBeatAnimation(Image heartImage)
    {
        if (heartImage == null) yield break;

        Transform heartTransform = heartImage.transform;

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
    /// 玩家重生后的处理
    /// </summary>
    private void OnPlayerRespawned()
    {
        // 重置护盾
        shieldHealth = 0;
        // 更新显示
        UpdateHealthDisplay();
    }
}