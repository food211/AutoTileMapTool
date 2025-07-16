using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameEvents;

/// <summary>
/// 管理玩家生命值、受伤和恢复的脚本
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField] public int maxHealth = 5;
    [SerializeField] public int currentHealth;

    [Header("护盾设置")]
    [SerializeField] private int maxShield = 5; // 最大护盾值
    [SerializeField] private int currentShield; // 当前护盾值
    
    [Header("无敌时间")]
    [SerializeField] private float invincibilityDuration = 1.5f; // 受伤后的无敌时间
    [SerializeField] private float respawnInvincibilityDuration = 3.0f; // 重生后的无敌时间

    [Header("生命值限制设置")]
    [SerializeField] private int maxHealthLimit; // 临时最大生命值限制
    private Coroutine healthLimitCoroutine; // 用于跟踪生命值限制的协程
    
    [Header("视觉效果")]
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private Color shieldDamageColor = new Color(0.3f, 0.6f, 1f); // 护盾受伤闪烁颜色
    [SerializeField] private float flashDuration = 0.1f;
    
    [Header("相机震动")]
    [SerializeField] private float minShakeIntensity = 0.1f; // 最小震动强度
    [SerializeField] private float maxShakeIntensity = 1.0f; // 最大震动强度
    
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private StatusManager statusManager;
    [SerializeField] private SpriteRenderer arrow;
    [SerializeField] private CheckpointManager checkpointManager; // 存档点管理器
    [SerializeField] private float respawnDelay = 1.0f; // 重生延迟
    
    // 内部变量
    private Color originalColor;
    private Coroutine invincibilityCoroutine;
    private Coroutine flashCoroutine;
    private Vector3 initialPosition; // 初始位置（仅作为备用）
    private bool isInvincible = false; // 是否处于无敌状态
    private bool isRespawning = false; // 标记玩家是否正在重生
    private int lastReportedHealth; // 用于跟踪上次报告的健康值
    private int lastReportedShield; // 用于跟踪上次报告的护盾值
    private bool isProcessingHealthChange = false; // 防止循环触发
    public bool debugmode = false; // 调试模式开关

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    #region 初始化方法

    /// <summary>
    /// 初始化组件和变量
    /// </summary>
    private void InitializeComponents()
    {
        // 初始化生命值
        currentHealth = maxHealth;
        lastReportedHealth = currentHealth;
        maxHealthLimit = maxHealth; // 初始化最大生命值限制为最大生命值

        // 初始化护盾值
        currentShield = 0; // 默认开始时没有护盾
        lastReportedShield = currentShield;
        // 确保最大护盾不超过最大生命值
        maxShield = Mathf.Min(maxShield, maxHealth);

        // 获取组件引用
        if (statusManager == null)
            statusManager = GetComponent<StatusManager>();

        if (playerRenderer == null)
            playerRenderer = GetComponent<SpriteRenderer>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (checkpointManager == null)
            checkpointManager = FindObjectOfType<CheckpointManager>();

        // 保存原始颜色
        if (playerRenderer != null)
            originalColor = playerRenderer.color;

        // 保存初始位置
        initialPosition = transform.position;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    private void SubscribeToEvents()
    {
        OnPlayerDamaged += TakeDamage;
        OnPlayerRespawn += RespawnPlayer;
        OnPlayerHealthChanged += HandleHealthChanged;
        OnPlayerShieldChanged += HandleShieldChanged;
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        OnPlayerDamaged -= TakeDamage;
        OnPlayerRespawn -= RespawnPlayer;
        OnPlayerHealthChanged -= HandleHealthChanged;
        OnPlayerShieldChanged -= HandleShieldChanged;
    }

    #endregion

    #region 生命值管理

    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        // 如果处于无敌状态或正在重生，不受伤害
        if (IsInvincible() || isRespawning)
            return;

        // 计算实际伤害和护盾吸收
        int shieldDamage = Mathf.Min(currentShield, damage);
        int healthDamage = damage - shieldDamage;

        // 减少护盾值
        if (shieldDamage > 0)
        {
            currentShield -= shieldDamage;

            // 触发护盾变化事件
            TriggerPlayerShieldChanged(currentShield, maxShield);
            lastReportedShield = currentShield;
        }

        // 减少生命值
        int previousHealth = currentHealth;
        if (healthDamage > 0)
        {
            currentHealth = Mathf.Max(0, currentHealth - healthDamage);

            // 只有当实际扣除生命值时才应用相机震动
            ApplyCameraShake(healthDamage);
        }

        // 防止重复处理
        isProcessingHealthChange = true;

        // 触发受伤事件
        TriggerPlayerHealthChanged(currentHealth, maxHealth);

        // 重置处理标志
        isProcessingHealthChange = false;
        lastReportedHealth = currentHealth;

        // 如果生命值为0，触发死亡
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 播放受伤视觉效果后再进入无敌状态
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            // 根据伤害类型选择闪烁效果
            if (shieldDamage > 0 && healthDamage == 0)
                flashCoroutine = StartCoroutine(ShieldDamageFlashThenInvincibility());
            else
                flashCoroutine = StartCoroutine(DamageFlashThenInvincibility());
        }
    }
    
    private IEnumerator ShieldDamageFlashThenInvincibility()
    {
        // 先执行护盾伤害闪烁
        yield return StartCoroutine(ShieldDamageFlashEffect());

        // 闪烁完成后再进入无敌状态
        SetInvincible(true);
    }

    private IEnumerator ShieldDamageFlashEffect()
    {
        if (playerRenderer == null) yield break;

        // 设置为蓝色
        playerRenderer.color = shieldDamageColor;

        // 等待闪烁时间
        yield return new WaitForSeconds(flashDuration);

        // 恢复原始颜色
        playerRenderer.color = originalColor;
    }

    /// <summary>
    /// 临时限制最大生命值
    /// </summary>
    /// <param name="limit">生命值上限</param>
    /// <param name="duration">限制持续时间（秒）</param>
    public void LimitMaxHealth(int limit, float duration)
    {
        // 确保限制在合理范围内
        int newLimit = Mathf.Clamp(limit, 1, maxHealth);

        // 如果已经有一个限制协程在运行，先停止它
        if (healthLimitCoroutine != null)
        {
            StopCoroutine(healthLimitCoroutine);
        }

        // 保存当前的最大生命值限制
        int previousLimit = maxHealthLimit;
        maxHealthLimit = newLimit;

        // 如果当前生命值超过新的限制，则减少生命值
        if (currentHealth > maxHealthLimit)
        {
            SetHealth(maxHealthLimit);
        }

        // 触发生命值上限变化事件
        TriggerPlayerMaxHealthLimitChanged(maxHealthLimit, maxHealth);

        // 开始计时协程
        healthLimitCoroutine = StartCoroutine(ResetMaxHealthLimitAfterDelay(duration));

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat($"临时限制最大生命值: {previousLimit} -> {maxHealthLimit}, 持续 {duration} 秒");
            #endif
    }

    /// <summary>
    /// 重置最大生命值限制的协程
    /// </summary>
    private IEnumerator ResetMaxHealthLimitAfterDelay(float delay)
    {
        // 等待指定的时间
        yield return new WaitForSeconds(delay);

        // 重置最大生命值限制
        int previousLimit = maxHealthLimit;
        maxHealthLimit = maxHealth;

        // 触发生命值上限变化事件
        TriggerPlayerMaxHealthLimitChanged(maxHealthLimit, maxHealth);

        // 清除协程引用
        healthLimitCoroutine = null;
#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.LogFormat($"最大生命值限制已解除: {previousLimit} -> {maxHealthLimit}");
        }
#endif
    }

    /// <summary>
    /// 立即重置最大生命值限制
    /// </summary>
    public void ResetMaxHealthLimit()
    {
        // 如果有限制协程在运行，停止它
        if (healthLimitCoroutine != null)
        {
            StopCoroutine(healthLimitCoroutine);
            healthLimitCoroutine = null;
        }

        // 重置最大生命值限制
        int previousLimit = maxHealthLimit;
        maxHealthLimit = maxHealth;

        // 触发生命值上限变化事件
        TriggerPlayerMaxHealthLimitChanged(maxHealthLimit, maxHealth);

#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.LogFormat($"最大生命值限制已手动解除: {previousLimit} -> {maxHealthLimit}");
        }
        #endif
    }
    
    /// <summary>
    /// 处理血量变化事件
    /// </summary>
    private void HandleHealthChanged(int newHealth, int maxHealth)
    {
        // 如果是自己触发的事件，则忽略
        if (isProcessingHealthChange)
            return;

        // 如果血量减少了
        if (newHealth < lastReportedHealth && !isRespawning)
        {
            int healthLoss = lastReportedHealth - newHealth;

            // 应用相机震动
            ApplyCameraShake(healthLoss);

            // 如果不是死亡，播放受伤效果
            if (newHealth > 0)
            {
                if (flashCoroutine != null)
                    StopCoroutine(flashCoroutine);

                flashCoroutine = StartCoroutine(DamageFlashEffect());
            }
        }

        // 更新上次报告的健康值
        lastReportedHealth = newHealth;
        currentHealth = newHealth; // 确保内部状态一致
    }

    /// <summary>
    /// 处理护盾值变化事件
    /// </summary>
    /// <param name="newShield">新的护盾值</param>
    /// <param name="maxShield">最大护盾值</param>
    private void HandleShieldChanged(int newShield, int maxShield)
    {
        // 如果是自己触发的事件，则忽略（防止循环触发）
        if (isProcessingHealthChange)
            return;

        // 保存旧的护盾值用于比较
        int previousShield = currentShield;

        // 更新内部护盾值
        currentShield = newShield;

        // 如果最大护盾值发生变化
        if (this.maxShield != maxShield)
        {
            // 更新最大护盾值，但确保不超过最大生命值
            this.maxShield = Mathf.Min(maxShield, maxHealth);
        }

        // 如果护盾值减少了（受到伤害）
        if (newShield < previousShield)
        {
            // 播放护盾受损视觉效果
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            flashCoroutine = StartCoroutine(ShieldDamageFlashEffect());
        }
        // 如果护盾值增加了（获得护盾）
        else if (newShield > previousShield)
        {
            // 播放获得护盾视觉效果
            StartCoroutine(ShieldAddedVisualEffect());
        }

        // 更新上次报告的护盾值
        lastReportedShield = newShield;

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("护盾值更新: {0} -> {1} / {2}", previousShield, newShield, this.maxShield);
#endif
    }
    
    /// <summary>
    /// 应用相机震动效果
    /// </summary>
    private void ApplyCameraShake(int damage)
    {
        // 计算血量损失的百分比（相对于最大生命值）
        float healthLossPercent = (float)damage / maxHealth;

        // 限制震动强度在设定范围内
        float shakeIntensity = Mathf.Clamp(healthLossPercent, minShakeIntensity, maxShakeIntensity);

        // 相机震动
        TriggerCameraShake(shakeIntensity);

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("血量变化: -{0}, 当前血量: {1}/{2}, 震动强度: {3:F2}",
                damage, currentHealth, maxHealth, shakeIntensity);
#endif
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    /// <param name="amount">恢复量</param>
    public void Heal(int amount)
    {
        // 增加生命值，但不超过最大值限制
        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealthLimit, currentHealth + amount);

        // 如果生命值没有变化，直接返回
        if (previousHealth == currentHealth)
            return;

        // 防止重复处理
        isProcessingHealthChange = true;

        // 触发生命值变化事件
        TriggerPlayerHealthChanged(currentHealth, maxHealth);

        // 重置处理标志
        isProcessingHealthChange = false;
        lastReportedHealth = currentHealth;

        // 如果不在重生过程中，播放恢复特效
        if (!isRespawning)
        {
            StartCoroutine(HealVisualEffect());
        }
    }
    
    /// <summary>
    /// 完全恢复生命值
    /// </summary>
    public void FullHeal()
    {
        Heal(maxHealth - currentHealth);
    }

    /// <summary>
    /// 设置当前生命值
    /// </summary>
    public void SetHealth(int health)
    {
        int previousHealth = currentHealth;
        // 使用maxHealthLimit而不是maxHealth作为上限
        currentHealth = Mathf.Clamp(health, 0, maxHealthLimit);

        // 如果生命值没有变化，直接返回
        if (previousHealth == currentHealth)
            return;

        // 防止重复处理
        isProcessingHealthChange = true;

        // 触发生命值变化事件
        TriggerPlayerHealthChanged(currentHealth, maxHealth);

        // 重置处理标志
        isProcessingHealthChange = false;
        lastReportedHealth = currentHealth;
    }

    /// <summary>
    /// 设置最大生命值
    /// </summary>
    public void SetMaxHealth(int health)
    {
        maxHealth = Mathf.Max(1, health);

        // 如果当前生命值超过最大值，调整为最大值
        if (currentHealth > maxHealth)
        {
            SetHealth(maxHealth);
        }

        // 确保最大护盾不超过最大生命值
        if (maxShield > maxHealth)
        {
            SetMaxShield(maxHealth);
        }
    }
    
    /// <summary>
    /// 获取当前生命值
    /// </summary>
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    /// <summary>
    /// 获取最大生命值
    /// </summary>
    public int GetMaxHealth()
    {
        return maxHealth;
    }
    
    #endregion
    #region 护盾管理

/// <summary>
/// 添加护盾
/// </summary>
/// <param name="amount">护盾量</param>
public void AddShield(int amount)
{
    if (amount <= 0) return;
    
    int previousShield = currentShield;
    // 确保不超过最大护盾值和最大生命值
    currentShield = Mathf.Min(maxShield, currentShield + amount);
    
    // 如果护盾值没有变化，直接返回
    if (previousShield == currentShield)
        return;
        
    // 触发护盾变化事件
    TriggerPlayerShieldChanged(currentShield, maxShield);
    lastReportedShield = currentShield;
    
    // 播放获得护盾的视觉效果
    StartCoroutine(ShieldAddedVisualEffect());
}

/// <summary>
/// 移除护盾
/// </summary>
/// <param name="amount">移除量</param>
public void RemoveShield(int amount)
{
    if (amount <= 0) return;
    
    int previousShield = currentShield;
    currentShield = Mathf.Max(0, currentShield - amount);
    
    // 如果护盾值没有变化，直接返回
    if (previousShield == currentShield)
        return;
        
    // 触发护盾变化事件
    TriggerPlayerShieldChanged(currentShield, maxShield);
    lastReportedShield = currentShield;
}

/// <summary>
/// 设置护盾值
/// </summary>
/// <param name="shield">护盾值</param>
public void SetShield(int shield)
{
    int previousShield = currentShield;
    currentShield = Mathf.Clamp(shield, 0, maxShield);
    
    // 如果护盾值没有变化，直接返回
    if (previousShield == currentShield)
        return;
        
    // 触发护盾变化事件
    TriggerPlayerShieldChanged(currentShield, maxShield);
    lastReportedShield = currentShield;
}

/// <summary>
/// 设置最大护盾值
/// </summary>
/// <param name="shield">最大护盾值</param>
public void SetMaxShield(int shield)
{
    // 确保最大护盾不超过最大生命值
    maxShield = Mathf.Min(Mathf.Max(0, shield), maxHealth);
    
    // 如果当前护盾值超过最大值，调整为最大值
    if (currentShield > maxShield)
    {
        SetShield(maxShield);
    }
    else
    {
        // 触发护盾变化事件以更新UI
        TriggerPlayerShieldChanged(currentShield, maxShield);
    }
}

/// <summary>
/// 获取当前护盾值
/// </summary>
public int GetCurrentShield()
{
    return currentShield;
}

/// <summary>
/// 获取最大护盾值
/// </summary>
public int GetMaxShield()
{
    return maxShield;
}

/// <summary>
/// 护盾添加的视觉效果
/// </summary>
private IEnumerator ShieldAddedVisualEffect()
{
    if (playerRenderer == null) yield break;
    
    // 保存原始颜色
    Color originalColor = playerRenderer.color;
    
    // 设置为蓝色
    playerRenderer.color = new Color(0.3f, 0.6f, 1f); // 蓝色
    
    // 等待闪烁时间
    yield return new WaitForSeconds(flashDuration);
    
    // 恢复原始颜色
    playerRenderer.color = originalColor;
}

    #endregion
    #region 无敌状态管理

    /// <summary>
    /// 设置无敌状态
    /// </summary>
    private void SetInvincible(bool invincible, float duration = -1)
    {
        isInvincible = invincible;
        
        // 如果启用无敌状态，开始无敌时间计时
        if (invincible)
        {
            if (invincibilityCoroutine != null)
                StopCoroutine(invincibilityCoroutine);
                
            // 使用指定的持续时间，如果未指定则使用默认值
            float actualDuration = duration > 0 ? duration : invincibilityDuration;
            invincibilityCoroutine = StartCoroutine(InvincibilityTimer(actualDuration));
        }
    }
    
    /// <summary>
    /// 检查是否处于无敌状态
    /// </summary>
    public bool IsInvincible()
    {
        return isInvincible || (playerController != null && playerController.IsInvincible());
    }
    
    /// <summary>
    /// 无敌时间计时器
    /// </summary>
    private IEnumerator InvincibilityTimer(float duration)
    {
        // 设置无敌状态
        isInvincible = true;
        
        // 如果不在重生过程中，才播放闪烁效果
        if (!isRespawning && playerController != null && !playerController.IsInRopeMode())
        {
            StartCoroutine(InvincibilityFlashEffect(duration));
        }
        
        // 等待无敌时间
        yield return new WaitForSeconds(duration);
        
        // 取消无敌状态
        isInvincible = false;
    }
    
    #endregion
    
    #region 视觉效果
    
    /// <summary>
    /// 先执行伤害闪烁，然后再进入无敌状态
    /// </summary>
    private IEnumerator DamageFlashThenInvincibility()
    {
        // 先执行伤害闪烁
        yield return StartCoroutine(DamageFlashEffect());
        
        // 闪烁完成后再进入无敌状态
        SetInvincible(true);
    }
    
    /// <summary>
    /// 受伤闪烁效果
    /// </summary>
    private IEnumerator DamageFlashEffect()
    {
        if (playerRenderer == null) yield break;
        
        // 设置为红色
        playerRenderer.color = damageFlashColor;
            
        // 等待闪烁时间
        yield return new WaitForSeconds(flashDuration);
        
        // 恢复原始颜色
        playerRenderer.color = originalColor;
    }
    
    /// <summary>
    /// 无敌状态闪烁效果
    /// </summary>
    private IEnumerator InvincibilityFlashEffect(float duration)
    {
        if (playerRenderer == null) yield break;
        
        float elapsed = 0f;
        float flashInterval = 0.1f;

        // 在无敌时间内闪烁
        while (elapsed < duration && !isRespawning)
        {
            // 检查是否处于电击状态
            bool isElectrifiedState = CheckIfElectrified();
            
            // 如果玩家处于电击状态，不改变透明度
            if (isElectrifiedState)
            {
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
                continue;
            }
            
            // 切换透明度
            float alpha = playerRenderer.color.a < 0.5f ? 1f : 0.3f;
            SetSpriteAlpha(alpha);
            
            // 等待闪烁间隔
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }
        
        // 如果不在重生过程中，恢复原始颜色
        if (!isRespawning && !CheckIfElectrified())
        {
            SetSpriteAlpha(1f);
        }
    }
    
    /// <summary>
    /// 设置精灵透明度
    /// </summary>
    private void SetSpriteAlpha(float alpha)
    {
        if (playerRenderer != null)
        {
            playerRenderer.color = new Color(
                originalColor.r,
                originalColor.g,
                originalColor.b,
                alpha
            );
        }
        
        if (arrow != null)
        {
            arrow.color = new Color(
                arrow.color.r,
                arrow.color.g,
                arrow.color.b,
                alpha
            );
        }
    }
    
    /// <summary>
    /// 检查是否处于电击状态
    /// </summary>
    private bool CheckIfElectrified()
    {
        if (statusManager == null) return false;
        
        return statusManager.IsInState(PlayerState.Electrified) || 
               statusManager.IsInState(PlayerState.Paralyzed);
    }
    
    /// <summary>
    /// 恢复生命值的视觉效果
    /// </summary>
    private IEnumerator HealVisualEffect()
    {
        if (playerRenderer == null) yield break;
        
        // 保存原始颜色
        Color originalColor = playerRenderer.color;
        
        // 设置为绿色
        playerRenderer.color = Color.green;
        
        // 等待闪烁时间
        yield return new WaitForSeconds(flashDuration);
        
        // 恢复原始颜色
        playerRenderer.color = originalColor;
    }
    
    #endregion
    
    #region 死亡和重生
    
    /// <summary>
    /// 玩家死亡处理
    /// </summary>
    private void Die()
    {
        // 清空护盾
        SetShield(0);
        // 触发玩家死亡事件
        TriggerPlayerDied();
        // 播放死亡动画或效果
        StartCoroutine(DeathSequence());
    }

    /// <summary>
    /// 立即杀死玩家（供编辑器按钮使用）
    /// </summary>
    public void KillPlayer()
    {
        // 只在运行时有效
        if (!Application.isPlaying)
            return;

        // 计算当前生命值作为伤害值
        int currentDamage = currentHealth;

        // 如果当前已经是0血量，设置一个默认伤害值以触发震动
        if (currentDamage == 0)
            currentDamage = maxHealth;

        // 应用相机震动（使用最大强度）
        ApplyCameraShake(currentDamage);

        // 设置生命值为0
        SetHealth(0);

        // 调用死亡处理
        Die();

#if UNITY_EDITOR
if (debugmode)
        Debug.LogFormat("玩家被立即杀死");
#endif
    }

    /// <summary>
    /// 死亡序列
    /// </summary>
    private IEnumerator DeathSequence()
    {
        if (playerRenderer == null) yield break;
        
        // 播放死亡动画 - 这里使用简单的闪烁和淡出效果
        float fadeTime = 1.5f;
        float elapsed = 0f;
        
        // 先闪烁几次
        for (int i = 0; i < 5; i++)
        {
            playerRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
        }
        
        // 然后淡出
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            SetSpriteAlpha(alpha);
            yield return null;
        }
        
        // 等待一小段时间
        yield return new WaitForSeconds(0.5f);
        
        // 触发重生事件
        TriggerPlayerRespawn();
    }
    
    /// <summary>
    /// 玩家重生处理
    /// </summary>
    public void RespawnPlayer()
    {
        // 开始重生序列
        StartCoroutine(RespawnSequence());
    }

    /// <summary>
    /// 重生序列
    /// </summary>
    private IEnumerator RespawnSequence()
    {
        // 设置重生标志
        isRespawning = true;

        // 确保玩家不可见
        SetSpriteAlpha(0f);

        // 等待短暂延迟
        yield return new WaitForSeconds(respawnDelay);

        // 确定重生位置并重置玩家
        Vector3 respawnPosition = GetRespawnPosition();
        ResetPlayerState(respawnPosition);

        // 等待短暂时间
        yield return new WaitForSeconds(0.5f);

        // 淡入效果
        yield return StartCoroutine(FadeInPlayer());

        // 设置无敌状态
        SetInvincible(true, respawnInvincibilityDuration);

        // 给玩家控制器也设置无敌状态
        if (playerController != null)
        {
            playerController.SetInvincible(true, respawnInvincibilityDuration);
            playerController.SetPlayerInput(true); // 重新启用玩家输入
        }

        // 触发状态重置事件
        TriggerPlayerStateChanged(PlayerState.Normal);

        // 重生完成，取消重生标志
        isRespawning = false;

        // 触发重生完成事件
        TriggerPlayerRespawnCompleted();
#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.LogFormat("玩家重生完成，位置: {0}", respawnPosition);
        } 
        #endif  
    }
    
    /// <summary>
    /// 获取重生位置
    /// </summary>
    private Vector3 GetRespawnPosition()
    {
        // 通过CheckpointManager获取适合的重生位置
        if (checkpointManager != null)
        {
            Vector3 position = checkpointManager.GetRespawnPosition();
            
            // 如果有激活的存档点，并且设置为重生时恢复生命值
            Checkpoint activeCheckpoint = checkpointManager.GetActiveCheckpoint();
            if (activeCheckpoint != null && activeCheckpoint.HealOnActivate)
            {
                // 恢复生命值但不显示特效
                SetHealth(maxHealth);
            }
            
            return position;
        }
        
        // 如果没有CheckpointManager，使用初始位置
        Debug.LogWarning("没有找到CheckpointManager，使用初始位置作为重生点");
        return initialPosition;
    }

    /// <summary>
    /// 重置玩家状态
    /// </summary>
    private void ResetPlayerState(Vector3 position)
    {
        // 重置玩家位置
        transform.position = position;

        // 重置物理状态
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 如果绳索系统处于活跃状态，释放绳索
        TriggerRopeReleased();

        // 恢复生命值（如果没有通过存档点恢复）
        if (currentHealth <= 0)
        {
            SetHealth(maxHealth);
        }
        // 重置护盾
        SetShield(0);
    }
    
    /// <summary>
    /// 淡入玩家
    /// </summary>
    private IEnumerator FadeInPlayer()
    {
        float fadeInTime = 1.0f;
        float elapsed = 0f;
        
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInTime);
            SetSpriteAlpha(alpha);
            yield return null;
        }
        
        // 确保完全不透明
        SetSpriteAlpha(1f);
    }
    
    #endregion
    
    /// <summary>
    /// 设置CheckpointManager引用
    /// </summary>
    public void SetCheckpointManager(CheckpointManager manager)
    {
        checkpointManager = manager;
    }
}