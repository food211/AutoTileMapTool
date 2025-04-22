using System.Collections;
using UnityEngine;


/// 状态管理器 - 负责处理玩家的各种状态效果

public class StatusManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private SpriteRenderer playerRenderer;
    
    [Header("状态效果设置")]
    [SerializeField] private float frozenDuration = 3.0f;
    [SerializeField] private float electrifiedDuration = 2.0f;
    [SerializeField] private int burningDamage = 1;
    [SerializeField] private float burningDrag = 0.8f;
    [SerializeField] private float burningDamageInterval = 1.5f;
    
    [Header("视觉效果")]
    [SerializeField] private GameObject frozenEffect;
    [SerializeField] private GameObject burningEffect;
    [SerializeField] private GameObject electrifiedEffect;
    public Color frozenTint = new Color(0.7f, 0.7f, 1.0f);
    public Color burningTint = new Color(1.0f, 0.6f, 0.6f);
    private Color electrifiedTint = new Color(1.0f, 1.0f, 0.6f);

    [Header("冷却时间设置")]
    [SerializeField] private float frozenCooldown = 2.0f; // 冰冻状态结束后的冷却时间
    [SerializeField] private float electrifiedCooldown = 1.5f; // 电击状态结束后的冷却时间
    // [Header("音效")]
    // [SerializeField] private AudioClip frozenSound;
    // [SerializeField] private AudioClip burningSound;
    // [SerializeField] private AudioClip electrifiedSound;
    // [SerializeField] private AudioClip statusEndSound;
    
    // 内部变量
    private GameEvents.PlayerState currentState = GameEvents.PlayerState.Normal;
    private Color originalTint;
    private float originalDrag;
    private float originalGravityScale;
    private bool originalKinematic;
    private Vector2 originalVelocity;
    private bool isFrozenOnCooldown = false;
    private bool isElectrifiedOnCooldown = false;
    
    // 协程引用
    private Coroutine currentStatusCoroutine;
    
    private void Awake()
    {
        // 获取组件引用
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
            
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();
            
        if (playerRenderer == null)
            playerRenderer = GetComponent<SpriteRenderer>();
            
        // 保存原始值
        if (playerRenderer != null)
            originalTint = playerRenderer.color;
            
        if (playerRigidbody != null)
        {
            originalDrag = playerRigidbody.drag;
            originalGravityScale = playerRigidbody.gravityScale;
            originalKinematic = playerRigidbody.isKinematic;
        }
    }
    
    private void OnEnable()
    {
        // 注册事件监听
        GameEvents.OnPlayerStateChanged += OnPlayerStateChanged;
    }
    
    private void OnDisable()
    {
        // 取消注册事件监听
        GameEvents.OnPlayerStateChanged -= OnPlayerStateChanged;
        
        // 确保协程被停止
        if (currentStatusCoroutine != null)
        {
            StopCoroutine(currentStatusCoroutine);
            currentStatusCoroutine = null;
        }
    }
    
#region State Change
    private void OnPlayerStateChanged(GameEvents.PlayerState newState)
    {
        // 如果状态没有变化，直接返回
        if (currentState == newState) return;
        
        // 记录前一个状态
        GameEvents.PlayerState previousState = currentState;
        currentState = newState;
        
        // 在控制台输出状态变化信息（调试用）
        #if UNITY_EDITOR
        Debug.LogFormat($"玩家状态从 {previousState} 变为 {newState}");
        #endif
        
        // 停止当前正在运行的状态协程
        if (currentStatusCoroutine != null)
        {
            StopCoroutine(currentStatusCoroutine);
            currentStatusCoroutine = null;
        }
        
        // 恢复默认设置，然后应用新状态的效果
        ResetToDefault();
        
        // 根据新状态应用相应的效果
        switch (newState)
        {
            case GameEvents.PlayerState.Normal:
                break;
                
            case GameEvents.PlayerState.Swinging:
                break;
                
            case GameEvents.PlayerState.Frozen:
                currentStatusCoroutine = StartCoroutine(ApplyFrozenState());
                break;
                
            case GameEvents.PlayerState.Burning:
                currentStatusCoroutine = StartCoroutine(ApplyBurningState());
                break;
                
            case GameEvents.PlayerState.Electrified:
                currentStatusCoroutine = StartCoroutine(ApplyElectrifiedState());
                break;
        }
    }
    
    
    /// 重置所有设置到默认状态
    
    private void ResetToDefault()
    {
        // 恢复玩家输入控制
        if (playerController != null)
            playerController.SetPlayerInput(true);
            
        // 恢复原始物理属性
        if (playerRigidbody != null)
        {
            playerRigidbody.drag = originalDrag;
            playerRigidbody.gravityScale = originalGravityScale;
            playerRigidbody.isKinematic = originalKinematic;
        }
        
        // 恢复原始视觉效果
        if (playerRenderer != null)
            playerRenderer.color = originalTint;
            
        // 关闭所有特效
        if (frozenEffect != null) frozenEffect.SetActive(false);
        if (burningEffect != null) burningEffect.SetActive(false);
        if (electrifiedEffect != null) electrifiedEffect.SetActive(false);
    }
    
    
    /// 应用冰冻状态
    
    private IEnumerator ApplyFrozenState()
    {
        // 保存当前速度
        if (playerRigidbody != null)
            originalVelocity = playerRigidbody.velocity;
        
        // 禁用玩家输入
        if (playerController != null)
            playerController.SetPlayerInput(false);
            
        // 应用冰冻视觉效果
        if (playerRenderer != null)
            playerRenderer.color = frozenTint;
            
        // 激活冰冻特效
        if (frozenEffect != null)
            frozenEffect.SetActive(true);

        // 冻结玩家
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.isKinematic = true;

        // 播放冰冻音效
        // PlaySound(frozenSound);
        
        // 等待冰冻持续时间
        yield return new WaitForSeconds(frozenDuration);
        
        // 如果状态仍然是冰冻，恢复正常状态
        if (currentState == GameEvents.PlayerState.Frozen)
        {
            // 恢复物理属性
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = originalKinematic;
                playerRigidbody.velocity = originalVelocity * 0.1f; // 恢复一部分速度
            }
            
            // 触发状态变化事件，回到正常状态或摆动状态
            if (playerController != null && playerController.IsInRopeMode())
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            }
            // 开始冰冻冷却时间
            StartCoroutine(FrozenCooldownRoutine());
        }
    }
    
    
    /// 应用燃烧状态
    
    private IEnumerator ApplyBurningState()
    {
        // 应用燃烧视觉效果
        if (playerRenderer != null)
            playerRenderer.color = burningTint;
            
        // 激活燃烧特效
        if (burningEffect != null)
            burningEffect.SetActive(true);
            
        // 应用燃烧物理效果
        if (playerRigidbody != null)
        {
            playerRigidbody.drag = burningDrag;
        }
        
        // 播放燃烧音效
        // PlaySound(burningSound);
        
        // 燃烧持续伤害
        float elapsedTime = 0f;
        float nextDamageTime = 0f;
        
        while (currentState == GameEvents.PlayerState.Burning)
        {
            elapsedTime += Time.deltaTime;
            
            // 定期造成伤害
            if (elapsedTime >= nextDamageTime)
            {
                // 造成伤害
                ApplyDamage(burningDamage);
                
                // 闪烁效果
                if (playerRenderer != null)
                {
                    playerRenderer.color = Color.red;
                    yield return new WaitForSeconds(0.1f);
                    playerRenderer.color = burningTint;
                }
                
                // 设置下一次伤害时间
                nextDamageTime = elapsedTime + burningDamageInterval;
            }
            
            yield return null;
        }
        
        // 如果状态仍然是燃烧，恢复正常状态
        if (currentState == GameEvents.PlayerState.Burning)
        {
            // 触发状态变化事件，断开绳子回到正常状态
            if (playerController != null && playerController.IsInRopeMode())
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            }
        }
    }
    
    
    /// 应用电击状态
    
    private IEnumerator ApplyElectrifiedState()
    {
        // 保存当前速度
        if (playerRigidbody != null)
            originalVelocity = playerRigidbody.velocity;


        // 禁用玩家输入
        if (playerController != null)
            playerController.SetPlayerInput(false);
            
        // 应用电击视觉效果
        if (playerRenderer != null)
            playerRenderer.color = electrifiedTint;
            
        // 激活电击特效
        if (electrifiedEffect != null)
            electrifiedEffect.SetActive(true);
            
        // 播放电击音效
        // PlaySound(electrifiedSound);

        playerRigidbody.velocity = Vector2.zero;
        // 保存原始位置
        Vector3 originalPosition = transform.position;
        
        // 电击抖动效果
        float elapsedTime = 0f;
        
        while (elapsedTime < electrifiedDuration && currentState == GameEvents.PlayerState.Electrified)
        {
            elapsedTime += Time.deltaTime;
            
            // 随机抖动效果
            if (playerRigidbody != null)
            {
                // 添加随机力模拟抖动
                Vector2 randomForce = new Vector2(
                    Random.Range(-10f, 10f),
                    Random.Range(-10f, 10f)
                );
                playerRigidbody.AddForce(randomForce, ForceMode2D.Impulse);
            }
            else
            {
                // 如果没有Rigidbody，直接移动Transform
                transform.position = originalPosition + new Vector3(
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.1f, 0.1f),
                    0
                );
            }
            
            // 闪烁效果
            if (playerRenderer != null)
            {
                playerRenderer.color = new Color(1.0f, 1.0f, 0.3f);
                yield return new WaitForSeconds(0.05f);
                playerRenderer.color = electrifiedTint;
            }
            
            yield return new WaitForSeconds(0.05f);
            
            // 更新原始位置（如果玩家在移动）
            originalPosition = new Vector3(transform.position.x, transform.position.y, originalPosition.z);
        }
        
        // 如果状态仍然是电击，恢复正常状态
        if (currentState == GameEvents.PlayerState.Electrified)
        {
            // 恢复物理属性
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = originalVelocity; // 恢复速度
            }
            // 触发状态变化事件，回到正常状态或摆动状态
            if (playerController != null && playerController.IsInRopeMode())
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Swinging);
            }
            else
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            
            StartCoroutine(ElectrifiedCooldownRoutine());
        }
    }


    private IEnumerator FrozenCooldownRoutine()
{
    // 设置冰冻冷却状态
    isFrozenOnCooldown = true;
    
    // 如果有PlayerController，通知它冰免疫状态
    if (playerController != null)
    {
        playerController.SetIceImmunity(true);
    }
    
    // 等待冷却时间
    yield return new WaitForSeconds(frozenCooldown);
    
    // 冷却结束
    isFrozenOnCooldown = false;
    
    // 如果有PlayerController，取消冰免疫状态
    if (playerController != null)
    {
        playerController.SetIceImmunity(false);
    }
}

private IEnumerator ElectrifiedCooldownRoutine()
{
    // 设置电击冷却状态
    isElectrifiedOnCooldown = true;
    
    // 如果有PlayerController，通知它电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(true);
    }
    
    // 等待冷却时间
    yield return new WaitForSeconds(electrifiedCooldown);
    
    // 冷却结束
    isElectrifiedOnCooldown = false;
    
    // 如果有PlayerController，取消电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(false);
    }
}
#endregion    
    
    /// 应用伤害
    
    private void ApplyDamage(int damage)
    {
        // 这里可以连接到玩家的生命值系统
        // 例如：playerHealth.TakeDamage(damage);
        
        // 触发伤害事件
        GameEvents.TriggerPlayerDamaged(damage);
        
        // 视觉反馈
        StartCoroutine(DamageFlashRoutine());
    }
    
    
    /// 伤害闪烁效果
    
    private IEnumerator DamageFlashRoutine()
    {
        if (playerRenderer == null) yield break;
        
        Color originalColor = playerRenderer.color;
        
        // 闪烁3次
        for (int i = 0; i < 3; i++)
        {
            playerRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    
    /// 播放音效
    
    // private void PlaySound(AudioClip clip)
    // {
    //     if (audioSource != null && clip != null)
    //     {
    //         audioSource.PlayOneShot(clip);
    //     }
    // }
public bool IsFrozenOnCooldown()
{
return isFrozenOnCooldown;
}

public bool IsElectrifiedOnCooldown()
{
    return isElectrifiedOnCooldown;
}
    
    /// 获取当前玩家状态
    
    public GameEvents.PlayerState GetCurrentState()
    {
        return currentState;
    }
    
    
    /// 检查玩家是否处于特定状态
    
    public bool IsInState(GameEvents.PlayerState state)
    {
        return currentState == state;
    }
}