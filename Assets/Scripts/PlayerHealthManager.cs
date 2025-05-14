using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理玩家生命值、受伤和恢复的脚本
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField] public int maxHealth = 5;
    [SerializeField] public int currentHealth;
    
    [Header("无敌时间")]
    [SerializeField] private float invincibilityDuration = 1.5f; // 受伤后的无敌时间
    [SerializeField] private float respawnInvincibilityDuration = 3.0f; // 重生后的无敌时间
    private bool isInvincible = false;
    
    [Header("视觉效果")]
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SpriteRenderer arrow;
    [SerializeField] private CheckpointManager checkpointManager; // 存档点管理器
    [SerializeField] private float respawnDelay = 1.0f; // 重生延迟
    
    // 内部变量
    private Color originalColor;
    private Coroutine invincibilityCoroutine;
    private Coroutine flashCoroutine;
    private Vector3 initialPosition; // 初始位置（仅作为备用）
    private bool isRespawning = false; // 标记玩家是否正在重生
    
    private void Awake()
    {
        // 初始化生命值为最大值
        currentHealth = maxHealth;
        
        // 获取组件引用
        if (playerRenderer == null)
            playerRenderer = GetComponent<SpriteRenderer>();
            
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
            
        if (checkpointManager == null)
            checkpointManager = FindObjectOfType<CheckpointManager>();
            
        // 保存原始颜色
        if (playerRenderer != null)
            originalColor = playerRenderer.color;
            
        // 保存初始位置（仅作为最后的备用）
        initialPosition = transform.position;
    }
    
    private void OnEnable()
    {
        // 订阅伤害事件
        GameEvents.OnPlayerDamaged += TakeDamage;
        // 订阅重生事件
        GameEvents.OnPlayerRespawn += RespawnPlayer;
    }
    
    private void OnDisable()
    {
        // 取消订阅伤害事件
        GameEvents.OnPlayerDamaged -= TakeDamage;
        // 取消订阅重生事件
        GameEvents.OnPlayerRespawn -= RespawnPlayer;
    }
    
    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        // 如果处于无敌状态或正在重生，不受伤害
        if (isInvincible || playerController.IsInvincible() || isRespawning)
            return;
            
        // 减少生命值
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // 触发受伤事件
        GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
        
        // 播放受伤视觉效果
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DamageFlashEffect());
        
        // 如果生命值为0，触发死亡
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 否则进入短暂无敌状态
            SetInvincible(true);
        }
    }
    
    /// <summary>
    /// 恢复生命值
    /// </summary>
    /// <param name="amount">恢复量</param>
    public void Heal(int amount)
    {
        // 增加生命值，但不超过最大值
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        
        // 触发生命值变化事件
        GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
        
        // 如果不在重生过程中，才播放恢复特效
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
        // 将生命值设置为最大值
        currentHealth = maxHealth;
        
        // 触发生命值变化事件
        GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
        
        // 如果不在重生过程中，才播放恢复特效
        if (!isRespawning)
        {
            StartCoroutine(HealVisualEffect());
        }
    }
    
    /// <summary>
    /// 玩家死亡处理
    /// </summary>
    private void Die()
    {
        // 触发玩家死亡事件
        GameEvents.TriggerPlayerDied();
            
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
            
        // 设置生命值为0
        currentHealth = 0;
        
        // 触发生命值变化事件
        GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
        
        // 调用死亡处理
        Die();
        
        #if UNITY_EDITOR
        Debug.Log("玩家被立即杀死");
        #endif
    }
    
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
    /// 无敌时间计时器
    /// </summary>
    private IEnumerator InvincibilityTimer(float duration)
    {
        // 设置无敌状态
        isInvincible = true;
        
        // 如果不在重生过程中，才播放闪烁效果
        if (!isRespawning)
        {
            StartCoroutine(InvincibilityFlashEffect(duration));
        }
        
        // 等待无敌时间
        yield return new WaitForSeconds(duration);
        
        // 取消无敌状态
        isInvincible = false;
    }
    
    /// <summary>
    /// 受伤闪烁效果
    /// </summary>
    private IEnumerator DamageFlashEffect()
    {
        // 设置为红色
        if (playerRenderer != null)
            playerRenderer.color = damageFlashColor;
            
        // 等待闪烁时间
        yield return new WaitForSeconds(flashDuration);
        
        // 恢复原始颜色
        if (playerRenderer != null)
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
            // 切换透明度
            playerRenderer.color = new Color(
                originalColor.r,
                originalColor.g,
                originalColor.b,
                playerRenderer.color.a < 0.5f ? 1f : 0.3f
            );
            
            // 如果有箭头，也让它闪烁
            if (arrow != null)
            {
                arrow.color = new Color(
                    arrow.color.r,
                    arrow.color.g,
                    arrow.color.b,
                    playerRenderer.color.a
                );
            }
            
            // 等待闪烁间隔
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }
        
        // 如果不在重生过程中，恢复原始颜色
        if (!isRespawning)
        {
            playerRenderer.color = originalColor;
            
            // 恢复箭头颜色
            if (arrow != null)
            {
                arrow.color = new Color(
                    arrow.color.r,
                    arrow.color.g,
                    arrow.color.b,
                    1f
                );
            }
        }
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
            playerRenderer.color = new Color(playerRenderer.color.r, playerRenderer.color.g, playerRenderer.color.b, alpha);
            
            if (arrow != null)
                arrow.color = new Color(arrow.color.r, arrow.color.g, arrow.color.b, alpha);
                
            yield return null;
        }
        
        // 等待一小段时间
        yield return new WaitForSeconds(0.5f);
        
        // 触发重生事件
        GameEvents.TriggerPlayerRespawn();
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
        if (playerRenderer != null)
            playerRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            
        if (arrow != null)
            arrow.color = new Color(arrow.color.r, arrow.color.g, arrow.color.b, 0f);
            
        // 等待短暂延迟
        yield return new WaitForSeconds(respawnDelay);
        
        // 确定重生位置
        Vector3 respawnPosition;
        
        // 通过CheckpointManager获取适合的重生位置
        if (checkpointManager != null)
        {
            respawnPosition = checkpointManager.GetRespawnPosition();
            
            // 如果有激活的存档点，并且设置为重生时恢复生命值
            Checkpoint activeCheckpoint = checkpointManager.GetActiveCheckpoint();
            if (activeCheckpoint != null && activeCheckpoint.HealOnActivate)
            {
                // 恢复生命值但不显示特效
                currentHealth = maxHealth;
                GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
            }
        }
        // 如果没有CheckpointManager，使用初始位置
        else
        {
            respawnPosition = initialPosition;
            Debug.LogWarning("没有找到CheckpointManager，使用初始位置作为重生点");
        }
        
        // 重置玩家位置
        transform.position = respawnPosition;
        
        // 重置物理状态
        if (GetComponent<Rigidbody2D>() != null)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // 如果绳索系统处于活跃状态，释放绳索
        GameEvents.TriggerRopeReleased();
        
        // 恢复生命值（如果没有通过存档点恢复）
        if (currentHealth <= 0)
        {
            // 恢复生命值但不显示特效
            currentHealth = maxHealth;
            GameEvents.TriggerPlayerHealthChanged(currentHealth, maxHealth);
        }
        
        // 等待短暂时间
        yield return new WaitForSeconds(0.5f);
        
        // 淡入效果
        float fadeInTime = 1.0f;
        float elapsed = 0f;
        
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInTime);
            
            if (playerRenderer != null)
                playerRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                
            if (arrow != null)
                arrow.color = new Color(arrow.color.r, arrow.color.g, arrow.color.b, alpha);
                
            yield return null;
        }
        
        // 确保完全不透明
        if (playerRenderer != null)
            playerRenderer.color = originalColor;
            
        if (arrow != null)
            arrow.color = new Color(arrow.color.r, arrow.color.g, arrow.color.b, 1f);
        
        // 设置短暂无敌时间
        SetInvincible(true, respawnInvincibilityDuration);
        
        // 给玩家控制器也设置无敌状态
        if (playerController != null)
        {
            playerController.SetInvincible(true, respawnInvincibilityDuration);
            // 重新启用玩家输入
            playerController.SetPlayerInput(true);
        }
   
        // 触发状态重置事件
        GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
        
        // 重生完成，取消重生标志
        isRespawning = false;
        
        // 触发重生完成事件
        GameEvents.TriggerPlayerRespawnCompleted();
        
        Debug.Log($"玩家在位置 {respawnPosition} 重生");
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
    
    /// <summary>
    /// 检查是否处于无敌状态
    /// </summary>
    public bool IsInvincible()
    {
        return isInvincible || playerController.IsInvincible();
    }
    
    /// <summary>
    /// 设置CheckpointManager引用
    /// </summary>
    public void SetCheckpointManager(CheckpointManager manager)
    {
        checkpointManager = manager;
    }
}