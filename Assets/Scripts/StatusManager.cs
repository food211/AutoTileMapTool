using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态管理器 - 负责处理玩家和绳索的各种状态效果
/// </summary>
public class StatusManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private SpriteRenderer playerRenderer;
    
    [Header("状态效果设置")]
    private bool isFireImmuneAfterFrozen = false; // 从冰冻解除后的火免疫状态
    private Coroutine fireImmuneCoroutine = null; // 火免疫协程
    private bool isFrozenElectrifiedVulnerable = false; // 冰冻后的电击易伤状态
    [SerializeField] private float frozenRopeBreakTime = 2.0f; // 冰冻状态下绳子断开的时间
    [SerializeField] private float totalFrozenDuration = 8.0f; // 绳子断开后继续冰冻的时间
    [SerializeField] private float fireImmunityAfterFrozenDuration = 4.0f; // 从冰冻解除后的火免疫持续时间
    [SerializeField] private float electrifiedDuration = 2.0f;
    [SerializeField] private int burningDamage = 1;
    [SerializeField] private float burningDrag = 0.8f;
    [SerializeField] private float burningDamageInterval = 1.5f;
    [SerializeField] private int struggletimes = 20;
    
    [Header("视觉效果")]
    [SerializeField] private Material frozenMaterial;
    [SerializeField] private Material burningMaterial;
    [SerializeField] private Material electrifiedMaterial;
    [SerializeField] private Material RopefrozenMaterial;
    [SerializeField] private Material RopeburningMaterial;
    [SerializeField] private Material RopeelectrifiedMaterial;
    [SerializeField] private GameObject fireParticleSystemPrefab; // 火焰粒子系统预制体
    private GameObject currentFireParticle; // 当前实例化的火焰粒子
    private Material originalMaterial;
    [ColorUsage(true, true)]
    public Color frozenTint = new Color(0.7f, 0.7f, 1.0f);
    [ColorUsage(true, true)]
    public Color burningTint = new Color(1.0f, 0.6f, 0.6f);
    [ColorUsage(true, true)]
    public Color electrifiedTint = new Color(1.0f, 1.0f, 0.6f);

    [Header("冷却时间设置")]
    [SerializeField] private float frozenCooldown = 2.0f; // 冰冻状态结束后的冷却时间
    [SerializeField] private float electrifiedCooldown = 1.5f; // 电击状态结束后的冷却时间



    // 状态枚举
    public enum PlayerState
    {
        Normal,
        Frozen,
        Burning,
        Electrified,
        Paralyzed // 麻痹状态（电击后的减速效果）
    }

    public enum RopeState
    {
        Normal,
        Burning,
        Electrified,
        Frozen
    }

    // 内部变量
    private PlayerState currentPlayerState = PlayerState.Normal;
    private RopeState currentRopeState = RopeState.Normal;
    private Color originalTint;
    private float originalDrag;
    private float originalGravityScale;
    private bool originalKinematic;
    private Vector2 originalVelocity;
    private bool isFrozenOnCooldown = false;
    private bool isElectrifiedOnCooldown = false;
    private bool isPlayerBurn = false;
    private bool isRopeActive = false; // 是否处于绳索模式
    
    // 协程引用
    private Coroutine currentPlayerStatusCoroutine;
    private Coroutine currentRopeStatusCoroutine;
    private Coroutine electrifiedBlinkCoroutine;

    private void Awake()
    {
        // 获取组件引用
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();

        if (playerRenderer == null)
            playerRenderer = GetComponent<SpriteRenderer>();

        if (ropeSystem == null)
            ropeSystem = playerController.GetComponentInChildren<RopeSystem>();

        // 保存原始值
        if (playerRenderer != null){
            originalTint = playerRenderer.color;
            originalMaterial = playerRenderer.material; }
            
        if (playerRigidbody != null)
        {
            originalDrag = playerRigidbody.drag;
            originalGravityScale = playerRigidbody.gravityScale;
            originalKinematic = playerRigidbody.isKinematic;
        }
    }

    #region Events
    private void OnEnable()
    {
        // 注册事件监听
        GameEvents.OnPlayerStateChanged += OnGameEventPlayerStateChanged;
        GameEvents.OnPlayerBurningStateChanged += SetPlayerBurn;
        GameEvents.OnRopeHooked += OnRopeHooked;
        GameEvents.OnRopeReleased += OnRopeReleased;
        GameEvents.OnPlayerDied += OnPlayerDied;
        
        // 添加对绳索颜色变化的监听
        GameEvents.OnPlayerStateChanged += ChangeRopeColor;
    }

    private void OnDisable()
    {
        // 取消注册事件监听
        GameEvents.OnPlayerStateChanged -= OnGameEventPlayerStateChanged;
        GameEvents.OnPlayerBurningStateChanged -= SetPlayerBurn;
        GameEvents.OnRopeHooked -= OnRopeHooked;
        GameEvents.OnRopeReleased -= OnRopeReleased;
        GameEvents.OnPlayerDied -= OnPlayerDied;
        
        // 取消对绳索颜色变化的监听
        GameEvents.OnPlayerStateChanged -= ChangeRopeColor;
        
        // 确保协程被停止
        if (currentPlayerStatusCoroutine != null)
        {
            StopCoroutine(currentPlayerStatusCoroutine);
            currentPlayerStatusCoroutine = null;
        }
        
        if (currentRopeStatusCoroutine != null)
        {
            StopCoroutine(currentRopeStatusCoroutine);
            currentRopeStatusCoroutine = null;
        }
    }

    private void OnPlayerDied()
    {
        // 停止所有协程 - 这只会停止StatusManager上的协程，不会影响其他脚本
        StopAllCoroutines();

        // 重置所有状态
        currentPlayerState = PlayerState.Normal;
        currentRopeState = RopeState.Normal;

        // 恢复玩家物理属性
        if (playerRigidbody != null)
        {
            playerRigidbody.drag = originalDrag;
            playerRigidbody.gravityScale = originalGravityScale;
            playerRigidbody.isKinematic = originalKinematic;
        }

        // 恢复玩家原始材质和颜色
        if (playerRenderer != null)
        {
            playerRenderer.material = originalMaterial;
            playerRenderer.color = originalTint;
        }

        // 销毁火焰粒子
        if (currentFireParticle != null)
        {
            FireParticleAutoSize autoSizer = currentFireParticle.GetComponent<FireParticleAutoSize>();
            if (autoSizer != null)
            {
                autoSizer.FadeOut(1.0f); // 死亡时更快地淡出火焰
            }
            else
            {
                Destroy(currentFireParticle);
            }
            currentFireParticle = null;
        }

        // 重置绳索状态和外观
        if (ropeSystem != null && ropeSystem.GetComponent<LineRenderer>() != null)
        {
            LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
            lineRenderer.startColor = ropeSystem.getOriginalColorGradiant().colorKeys[0].color;
            lineRenderer.endColor = ropeSystem.getOriginalColorGradiant().colorKeys[1].color;
            lineRenderer.colorGradient = ropeSystem.getOriginalColorGradiant();
            lineRenderer.widthCurve = ropeSystem.getOriginalCurve();
        }

        // 清除所有状态标志
        isFireImmuneAfterFrozen = false;
        isFrozenElectrifiedVulnerable = false;
        isFrozenOnCooldown = false;
        isElectrifiedOnCooldown = false;
        isPlayerBurn = false;

        // 清除协程引用
        currentPlayerStatusCoroutine = null;
        currentRopeStatusCoroutine = null;
        electrifiedBlinkCoroutine = null;
        fireImmuneCoroutine = null;

        // 确保绳索释放
        isRopeActive = false;

        // 通知GameEvents系统状态已重置
        GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
        GameEvents.TriggerRopeStateChanged(GameEvents.RopeState.Normal);

#if UNITY_EDITOR
        Debug.LogFormat("玩家死亡: 所有状态效果已清除");
#endif
    }


    // 绳索钩住事件处理
    private void OnRopeHooked(Vector2 hookPosition)
    {
        isRopeActive = true;
        
        // 检查钩住的物体类型，设置相应的绳索状态
        string hookTag = ropeSystem.GetCurrentHookTag();
        SetRopeStateBasedOnTag(hookTag);
    }

    // 绳索释放事件处理
    private void OnRopeReleased()
    {
        isRopeActive = false;
        
        // 重置绳索状态
        SetRopeState(RopeState.Normal);
        
        // 如果玩家状态是由绳索引起的，考虑是否需要重置
        if (currentPlayerState == PlayerState.Electrified && 
            playerController.IsHookingElectrifiedObject())
        {
            // 如果是由于钩住电物体导致的电击，释放绳索后转为麻痹状态
            SetPlayerState(PlayerState.Paralyzed);
        }
    }
    #endregion

    #region State Management
    // 处理来自GameEvents的状态变化事件
    private void OnGameEventPlayerStateChanged(GameEvents.PlayerState gameEventState)
    {
        // 将GameEvents.PlayerState转换为内部PlayerState
        PlayerState newState = ConvertGameEventStateToPlayerState(gameEventState);
        
        // 设置玩家状态
        SetPlayerState(newState);
        
        // 如果在绳索模式下，可能还需要设置绳索状态
        if (isRopeActive)
        {
            RopeState ropeState = DetermineRopeStateFromPlayerState(newState);
            SetRopeState(ropeState);
        }
    }

    // 将GameEvents.PlayerState转换为内部PlayerState
    private PlayerState ConvertGameEventStateToPlayerState(GameEvents.PlayerState gameEventState)
    {
        switch (gameEventState)
        {
            case GameEvents.PlayerState.Normal:
                return PlayerState.Normal;
            case GameEvents.PlayerState.Frozen:
                return PlayerState.Frozen;
            case GameEvents.PlayerState.Burning:
                return PlayerState.Burning;
            case GameEvents.PlayerState.Electrified:
                return PlayerState.Electrified;
            case GameEvents.PlayerState.Swinging:
                // Swinging不是玩家状态，而是绳索模式的一种表现
                return PlayerState.Normal;
            default:
                return PlayerState.Normal;
        }
    }

    // 根据玩家状态确定绳索状态
    private RopeState DetermineRopeStateFromPlayerState(PlayerState playerState)
    {
        switch (playerState)
        {
            case PlayerState.Frozen:
                return RopeState.Frozen;
            case PlayerState.Burning:
                return RopeState.Burning;
            case PlayerState.Electrified:
                return RopeState.Electrified;
            default:
                return RopeState.Normal;
        }
    }

    // 根据标签设置绳索状态
        public void SetRopeStateBasedOnTag(string tag)
    {
        switch (tag)
        {
            case "Ice":
                if (!playerController.IsIceImmune() && !isFrozenOnCooldown)
                {
                    SetRopeState(RopeState.Frozen);
                    SetPlayerState(PlayerState.Frozen);
                }
                else
                {
                    // 在冰冻免疫期间，绳索保持正常状态但有视觉提示
                    SetRopeState(RopeState.Normal);
                    
                    // 添加视觉提示
                    if (ropeSystem != null)
                    {
                        LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                        if (lineRenderer != null)
                        {
                            // 设置一个轻微的蓝色调，表示免疫
                            Color immuneColor = new Color(0.7f, 0.8f, 1.0f);
                            lineRenderer.startColor = immuneColor;
                            lineRenderer.endColor = immuneColor;
                        }
                    }
                }
                break;
                
            case "Fire":
                // 考虑从冰冻状态解除后的火免疫
                if (isFireImmuneAfterFrozen || playerController.IsFireImmune())
                {
                    // 在火免疫期间，绳索保持正常状态但有视觉提示
                    SetRopeState(RopeState.Normal);
                    
                    // 添加视觉提示
                    if (ropeSystem != null)
                    {
                        LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                        if (lineRenderer != null)
                        {
                            // 设置一个轻微的蓝色调，表示火免疫
                            Color immuneColor = new Color(0.7f, 0.9f, 1.0f);
                            lineRenderer.startColor = immuneColor;
                            lineRenderer.endColor = immuneColor;
                        }
                    }
                }
                else
                {
                    SetRopeState(RopeState.Burning);
                    if (!playerController.IsFireImmune())
                    {
                        SetPlayerState(PlayerState.Burning);
                    }
                }
                break;
                
            case "Elect":
                if (!playerController.IsElectricImmune() && !isElectrifiedOnCooldown)
                {
                    SetRopeState(RopeState.Electrified);
                    SetPlayerState(PlayerState.Electrified);
                }
                else
                {
                    // 在电击免疫期间，绳索保持正常状态但有视觉提示
                    SetRopeState(RopeState.Normal);
                    
                    // 添加视觉提示
                    if (ropeSystem != null)
                    {
                        LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                        if (lineRenderer != null)
                        {
                            // 设置一个轻微的黄色调，表示电击免疫
                            Color immuneColor = new Color(1.0f, 1.0f, 0.8f);
                            lineRenderer.startColor = immuneColor;
                            lineRenderer.endColor = immuneColor;
                        }
                    }
                }
                break;
                
            default:
                SetRopeState(RopeState.Normal);
                break;
        }
    }


    // 设置玩家状态
    public void SetPlayerState(PlayerState newState)
    {
        // 如果状态没有变化，直接返回
        if (currentPlayerState == newState) return;
        
        // 检查状态优先级和冷却
        if (!CanChangeToState(newState)) return;
        
        // 记录前一个状态
        PlayerState previousState = currentPlayerState;
        currentPlayerState = newState;
        
        // 在控制台输出状态变化信息（调试用）
        #if UNITY_EDITOR
        Debug.LogFormat($"玩家状态从 {previousState} 变为 {newState}");
        #endif
        
        // 停止当前正在运行的状态协程
        if (currentPlayerStatusCoroutine != null)
        {
            StopCoroutine(currentPlayerStatusCoroutine);
            currentPlayerStatusCoroutine = null;
        }
        
        // 恢复默认设置，然后应用新状态的效果
        ResetPlayerToDefault();
        
        // 根据新状态应用相应的效果
        switch (newState)
        {
            case PlayerState.Normal:
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
                break;
                
            case PlayerState.Frozen:
                currentPlayerStatusCoroutine = StartCoroutine(ApplyFrozenState());
                break;
                
            case PlayerState.Burning:
                currentPlayerStatusCoroutine = StartCoroutine(ApplyBurningState());
                break;
                
            case PlayerState.Electrified:
                currentPlayerStatusCoroutine = StartCoroutine(ApplyElectrifiedState());
                break;
                
            case PlayerState.Paralyzed:
                currentPlayerStatusCoroutine = StartCoroutine(ApplyParalyzedState());
                break;
        }
    }

    // 设置绳索状态
    public void SetRopeState(RopeState newState)
    {
        // 如果状态没有变化或绳索不活跃，直接返回
        if (currentRopeState == newState || !isRopeActive) return;
        
        // 记录前一个状态
        RopeState previousState = currentRopeState;
        currentRopeState = newState;
        
        // 在控制台输出状态变化信息（调试用）
        #if UNITY_EDITOR
        Debug.LogFormat($"绳索状态从 {previousState} 变为 {newState}");
        #endif
        
        // 停止当前正在运行的状态协程
        if (currentRopeStatusCoroutine != null)
        {
            StopCoroutine(currentRopeStatusCoroutine);
            currentRopeStatusCoroutine = null;
        }
        
        // 根据新状态应用相应的效果
        switch (newState)
        {
            case RopeState.Normal:
                // 恢复绳索正常状态
                if (ropeSystem != null)
                {
                    // 重置绳索视觉效果
                    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        lineRenderer.startColor = ropeSystem.getOriginalColorGradiant().colorKeys[0].color;
                        lineRenderer.endColor = ropeSystem.getOriginalColorGradiant().colorKeys[1].color;
                        lineRenderer.widthCurve = ropeSystem.getOriginalCurve();
                    }
                }
                GameEvents.TriggerRopeStateChanged(GameEvents.RopeState.Normal);
                break;
                
            case RopeState.Frozen:
                // 应用冰冻效果到绳索
                if (ropeSystem != null)
                {
                    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        lineRenderer.startColor = frozenTint;
                        lineRenderer.endColor = frozenTint;
                    }
                }
                GameEvents.TriggerRopeStateChanged(GameEvents.RopeState.Frozen);
                break;
                
            case RopeState.Burning:
                // 开始绳索燃烧效果
                currentRopeStatusCoroutine = StartCoroutine(BurnRopeEffect(ropeSystem));
                GameEvents.TriggerRopeStateChanged(GameEvents.RopeState.Burning);
                break;
                
            case RopeState.Electrified:
                // 应用电击效果到绳索
                if (ropeSystem != null)
                {
                    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        lineRenderer.startColor = electrifiedTint;
                        lineRenderer.endColor = electrifiedTint;
                    }
                }
                GameEvents.TriggerRopeStateChanged(GameEvents.RopeState.Electrified);
                break;
        }
    }

    // 检查是否可以切换到新状态（考虑优先级和冷却）
    private bool CanChangeToState(PlayerState newState)
    {
        // 如果当前是冰冻状态，只有解冻后才能切换到其他状态
        // 但允许从冰冻直接转为电击状态（冰冻导致电击易伤）
        if (currentPlayerState == PlayerState.Frozen && 
            newState != PlayerState.Normal && 
            newState != PlayerState.Electrified)
            return false;
            
        // 如果当前是电击状态，只有电击结束后才能切换到其他状态
        if (currentPlayerState == PlayerState.Electrified && 
            newState != PlayerState.Normal && 
            newState != PlayerState.Paralyzed)
            return false;
            
        // 检查冷却状态
        if (newState == PlayerState.Frozen && isFrozenOnCooldown)
            return false;
            
        if (newState == PlayerState.Electrified && isElectrifiedOnCooldown)
            return false;
            
        // 检查免疫状态
        if (newState == PlayerState.Frozen && playerController.IsIceImmune())
            return false;
            
        // 检查火免疫状态（包括从冰冻解除后的特殊火免疫）
        if (newState == PlayerState.Burning && 
            (playerController.IsFireImmune() || isFireImmuneAfterFrozen))
            return false;
            
        if (newState == PlayerState.Electrified && playerController.IsElectricImmune())
            return false;
            
        return true;
    }

    // 重置玩家到默认状态
    public void ResetPlayerToDefault()
    {
        // 恢复玩家输入控制
        if (playerController != null)
            playerController.SetPlayerMove(true);
            
        // 恢复原始物理属性
        if (playerRigidbody != null)
        {
            playerRigidbody.drag = originalDrag;
            playerRigidbody.gravityScale = originalGravityScale;
            playerRigidbody.isKinematic = originalKinematic;
        }
        
        // 恢复原始视觉效果，但保留特殊免疫状态的视觉效果
        if (playerRenderer != null)
        {
            if (isFireImmuneAfterFrozen)
            {
                // 保持火免疫的视觉效果
                playerRenderer.color = new Color(0.7f, 0.9f, 1.0f);
            }
            else
            {
                playerRenderer.color = originalTint;
                playerRenderer.material = originalMaterial;
            }
        }
    }
    #endregion

    #region Player State Effects
    // 应用冰冻状态
    private IEnumerator ApplyFrozenState()
    {
        // 保存当前速度
        if (playerRigidbody != null)
            originalVelocity = playerRigidbody.velocity;
        
        // 禁用玩家输入
        if (playerController != null)
        {
            playerController.SetPlayerMove(false);
            GameEvents.TriggerCanShootRopeChanged(false);
        }
            
        // 应用冰冻视觉效果
        if (playerRenderer != null)
        {
            playerRenderer.color = frozenTint;
            if (frozenMaterial != null)
                playerRenderer.material = frozenMaterial;
        }

        // 冻结玩家
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.isKinematic = true;

        // 设置电击易伤状态
        isFrozenElectrifiedVulnerable = true;

        // 通知GameEvents系统状态已变更
        GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Frozen);
        
        // 冰冻时间计时器
        float elapsedTime = 0f;
        bool isFrozenBroken = false;
        bool ropeBreakTriggered = false; // 是否已触发绳索断开
        
        // 等待冰冻持续时间，同时检测是否接触火表面
        while (elapsedTime < totalFrozenDuration && 
            currentPlayerState == PlayerState.Frozen && 
            !isFrozenBroken)
        {
            elapsedTime += Time.deltaTime;
            
            // 检查是否到达绳索断开时间点
            if (!ropeBreakTriggered && elapsedTime >= frozenRopeBreakTime)
            {
                // 如果绳索处于活跃状态，断开绳索
                if (isRopeActive)
                {
                    GameEvents.TriggerRopeReleased();
                    #if UNITY_EDITOR
                    Debug.LogFormat("冰冻状态持续2秒，自动断开绳索，但玩家仍保持冰冻");
                    #endif
                    
                    // 修改：绳索断开后，允许玩家自由下落
                    if (playerRigidbody != null)
                    {
                        playerRigidbody.isKinematic = false; // 取消运动学状态
                        playerRigidbody.velocity = Vector2.zero; // 重置速度
                        playerRigidbody.gravityScale = originalGravityScale; // 恢复重力
                    }
                }
                ropeBreakTriggered = true;
            }
            
            // 检测是否接触火表面
            if (isPlayerBurn)
            {
                #if UNITY_EDITOR
                Debug.LogFormat("冰冻状态被火解除！获得火免疫");
                #endif
                isFrozenBroken = true;
                
                // 解除冰冻状态
                if (playerRigidbody != null)
                {
                    playerRigidbody.isKinematic = originalKinematic;
                    playerRigidbody.velocity = originalVelocity * 0.3f; // 恢复更多速度，因为是被火解除
                }
                
                // 给予火免疫状态
                StartFireImmunityAfterFrozen();
                
                // 如果绳索处于活跃状态且尚未断开，断开绳索
                if (isRopeActive && !ropeBreakTriggered)
                {
                    GameEvents.TriggerRopeReleased();
                }
                
                // 重置玩家状态
                SetPlayerState(PlayerState.Normal);
                
                // 开始冰冻冷却时间
                StartCoroutine(FrozenCooldownRoutine());
                
                // 退出循环
                break;
            }
            
            // 检测是否接触电表面
            Vector2 playerPosition = transform.position;
            Collider2D[] electColliders = Physics2D.OverlapCircleAll(playerPosition, 0.5f);
            foreach (Collider2D collider in electColliders)
            {
                if (collider.CompareTag("Elect") && !playerController.IsElectricImmune())
                {
                    #if UNITY_EDITOR
                    Debug.LogFormat("冰冻状态下接触电表面！电击伤害加倍");
                    #endif
                    isFrozenBroken = true;
                    
                    // 如果绳索处于活跃状态且尚未断开，断开绳索
                    if (isRopeActive && !ropeBreakTriggered)
                    {
                        GameEvents.TriggerRopeReleased();
                    }
                    
                    // 转为电击状态
                    SetPlayerState(PlayerState.Electrified);
                    
                    // 退出循环
                    break;
                }
            }
            
            yield return null;
        }
        
        // 如果状态仍然是冰冻（正常结束冰冻时间），恢复正常状态
        if (currentPlayerState == PlayerState.Frozen && !isFrozenBroken)
        {
            #if UNITY_EDITOR
            Debug.LogFormat("冰冻状态自然结束，恢复正常状态");
            #endif
            
            // 恢复物理属性
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = originalKinematic;
                playerRigidbody.velocity = originalVelocity * 0.1f; // 恢复一部分速度
            }
            
            // 如果绳索处于活跃状态且尚未断开，断开绳索
            if (isRopeActive && !ropeBreakTriggered)
            {
                GameEvents.TriggerRopeReleased();
                #if UNITY_EDITOR
                Debug.LogFormat("冰冻结束，自动断开绳索");
                #endif
            }
            
            // 开始冰冻冷却时间
            StartCoroutine(FrozenCooldownRoutine());
            
            // 重置电击易伤状态
            isFrozenElectrifiedVulnerable = false;
            
            // 恢复玩家输入控制
            if (playerController != null)
            {
                playerController.SetPlayerMove(true);
                GameEvents.TriggerCanShootRopeChanged(true);
            }

            // 恢复原始颜色
            if (playerRenderer != null)
            {
                playerRenderer.material = originalMaterial;
                playerRenderer.color = originalTint;
            }
                
            // 设置为正常状态
            SetPlayerState(PlayerState.Normal);
        }
    }



    private void StartFireImmunityAfterFrozen()
    {
        // 停止之前的火免疫协程（如果存在）
        if (fireImmuneCoroutine != null)
        {
            StopCoroutine(fireImmuneCoroutine);
        }
        
        // 设置火免疫状态
        isFireImmuneAfterFrozen = true;
        if (playerController != null)
        {
            playerController.SetFireImmunity(true);
        }
        
        // 添加视觉效果提示
        if (playerRenderer != null)
        {
            // 添加一个淡蓝色的色调，表示从冰冻状态解除后的火免疫
            Color fireImmuneColor = new Color(0.7f, 0.9f, 1.0f);
            playerRenderer.color = fireImmuneColor;
        }
        
        // 启动火免疫协程
        fireImmuneCoroutine = StartCoroutine(FireImmunityAfterFrozenRoutine());
    }

    // 火免疫协程
    private IEnumerator FireImmunityAfterFrozenRoutine()
    {
        #if UNITY_EDITOR
        Debug.LogFormat("获得火免疫，持续 {0} 秒", fireImmunityAfterFrozenDuration);
        #endif

        // 添加火免疫期间的白色闪烁效果
        StartCoroutine(CooldownBlinkEffect(fireImmunityAfterFrozenDuration));
        
        // 等待火免疫时间
        yield return new WaitForSeconds(fireImmunityAfterFrozenDuration);
        
        // 火免疫结束
        isFireImmuneAfterFrozen = false;
        if (playerController != null)
        {
            playerController.SetFireImmunity(false);
        }
        
        // 恢复原始颜色和材质
        if (playerRenderer != null && currentPlayerState == PlayerState.Normal)
        {
            playerRenderer.color = originalTint;
            playerRenderer.material = originalMaterial;
        }
        #if UNITY_EDITOR
        Debug.LogFormat("火免疫结束");
        #endif
        
        // 清空协程引用
        fireImmuneCoroutine = null;
    }

// 修改电击状态处理，增加冰冻后的易伤机制
    private IEnumerator ApplyElectrifiedState()
    {
        // 保存当前速度
        if (playerRigidbody != null)
            originalVelocity = playerRigidbody.velocity;

        // 判断是否是直接钩中带电物体
        bool isHookingElectrified = playerController != null && playerController.IsHookingElectrifiedObject();
        
        // 禁用玩家输入和发射绳索能力
        if (playerController != null)
        {
            playerController.SetPlayerMove(false);
            GameEvents.TriggerCanShootRopeChanged(false);
        }
            
        // 应用电击视觉效果
        if (playerRenderer != null)
        {
            playerRenderer.color = electrifiedTint;
            if (electrifiedMaterial != null)
                playerRenderer.material = electrifiedMaterial;
        }

        // 通知GameEvents系统状态已变更
        GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Electrified);

        playerRigidbody.velocity = Vector2.zero;
        // 保存原始位置
        Vector3 originalPosition = transform.position;
        
        // 记录开始时间，而不是累加时间
        float startTime = Time.time;
        float lastDamageTime = startTime;
        float lastVisualUpdateTime = startTime;
        
        // 挣脱计数器 - 仅在直接钩中带电物体时使用
        int struggleCount = 0;
        int requiredStruggles = isFrozenElectrifiedVulnerable ? struggletimes * 2 : struggletimes; // 冰冻后电击需要更多次数挣脱
        bool keyWasPressed = false; // 用于跟踪上一帧是否按下了空格键
        
        // 启动单独的视觉效果协程
        StartCoroutine(ElectrifiedVisualEffects());
        
        // 如果从冰冻状态转为电击状态，显示提示
        if (isFrozenElectrifiedVulnerable)
        {
            #if UNITY_EDITOR
            Debug.LogFormat("冰冻导致电击易伤！挣脱难度增加，伤害加倍");
            #endif
        }
        
        // 如果是直接钩中带电物体，则持续电击直到挣脱；否则持续到electrifiedDuration结束
        while (((isHookingElectrified && struggleCount < requiredStruggles) || 
            (!isHookingElectrified && Time.time - startTime < electrifiedDuration)) && 
            currentPlayerState == PlayerState.Electrified)
        {
            // 检测空格键输入 - 仅在直接钩中带电物体时
            if (isHookingElectrified)
            {
                // 使用GetKeyDown而不是GetKeyUp，提高响应性
                if (Input.GetKeyDown(KeyCode.Space) && !keyWasPressed)
                {
                    keyWasPressed = true;
                    struggleCount++;
                    
                    // 显示挣脱进度提示
                    #if UNITY_EDITOR
                    Debug.LogFormat($"挣脱进度: {struggleCount}/{requiredStruggles}");
                    #endif
                    
                    // 可以在这里添加挣脱进度的视觉反馈
                    if (playerRenderer != null)
                    {
                        // 挣脱时闪烁一下白色
                        playerRenderer.color = Color.white;
                    }
                }
                else if (!Input.GetKey(KeyCode.Space))
                {
                    keyWasPressed = false;
                }
            }
            
            // 随机抖动效果 - 降低频率以减少性能开销
            if (Time.time - lastVisualUpdateTime > 0.05f)
            {
                if (playerRigidbody != null)
                {
                    // 添加随机力模拟抖动
                    Vector2 randomForce = new Vector2(
                        Random.Range(-5f, 5f),
                        Random.Range(-5f, 5f)
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
                
                lastVisualUpdateTime = Time.time;
            }
            
            // 更新原始位置（如果玩家在移动）
            originalPosition = new Vector3(transform.position.x, transform.position.y, originalPosition.z);
            
            // 如果是钩中状态，定期对玩家造成伤害（每2秒一次）
            float currentTime = Time.time;
            if (isHookingElectrified && currentTime - lastDamageTime >= 2.0f)
            {
                // 冰冻后电击伤害加倍
                int damageAmount = isFrozenElectrifiedVulnerable ? 2 : 1;
                GameEvents.TriggerPlayerDamaged(damageAmount);
                lastDamageTime = currentTime;
            }
            
            // 使用非常短的等待时间，以保持输入检测的高频率
            yield return new WaitForSeconds(0.01f);
        }
        
        // 如果状态仍然是电击，处理状态转换
        if (currentPlayerState == PlayerState.Electrified)
        {
            // 恢复物理属性
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = originalVelocity * 0.5f; // 恢复一半速度
            }
            
            // 重置电击易伤状态
            isFrozenElectrifiedVulnerable = false;
            
            // 如果是直接钩中带电物体并成功挣脱
            if (isHookingElectrified && struggleCount >= requiredStruggles)
            {
                // 释放绳索
                GameEvents.TriggerRopeReleased();
                
                // 转为麻痹状态
                SetPlayerState(PlayerState.Paralyzed);
                
                // 可以添加一个成功挣脱的视觉或音效反馈
                Debug.LogFormat("成功挣脱电击！进入麻痹状态");
            }
            else if (!isHookingElectrified)
            {
                // 如果不是直接钩中带电物体，开始电击冷却
                StartCoroutine(ElectrifiedCooldownRoutine());
                GameEvents.TriggerRopeReleased();

                SetPlayerState(PlayerState.Paralyzed);
            }
            else
            {
                // 如果是直接钩中带电物体但没有成功挣脱，保持电击状态
                // 重置挣脱计数，给玩家一个重新开始的机会
                struggleCount = 0;
                
                // 重新启动电击状态协程
                currentPlayerStatusCoroutine = StartCoroutine(ApplyElectrifiedState());
                yield break; // 提前返回，避免执行后续代码
            }
        }
    }
    // 应用燃烧状态
    private IEnumerator ApplyBurningState()
{
    // 应用燃烧视觉效果
    if (playerRenderer != null && isPlayerBurn)
    {
        playerRenderer.color = burningTint;
        if (burningMaterial != null)
            playerRenderer.material = burningMaterial;

        // 创建火焰粒子效果
        if (fireParticleSystemPrefab != null && currentFireParticle == null)
        {
            currentFireParticle = Instantiate(fireParticleSystemPrefab, transform);
            
            // 使用FireParticleAutoSize组件自动调整大小
            FireParticleAutoSize autoSizer = currentFireParticle.GetComponent<FireParticleAutoSize>();
            if (autoSizer != null)
            {
                autoSizer.AdjustToTarget(playerRenderer);
            }
        }
    }

    // 禁用玩家松开绳子
    if (playerController != null)
        GameEvents.TriggerCanShootRopeChanged(false);

    // 应用燃烧物理效果
    if (playerRigidbody != null)
    {
        playerRigidbody.drag = burningDrag;
    }

    // 通知GameEvents系统状态已变更
    GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Burning);

    // 燃烧持续伤害
    float elapsedTime = 0f;
    float nextDamageTime = 0f;

    // 玩家离开火物体后的燃烧计时器
    float playerBurnTimer = 0f;
    float maxPlayerBurnTime = 4.0f; // 最大燃烧时间，4秒
    bool wasPlayerBurning = false;

    // 只要玩家处于燃烧状态，就继续循环
    while (currentPlayerState == PlayerState.Burning &&
           (isPlayerBurn || wasPlayerBurning))
    {
        elapsedTime += Time.deltaTime;

        // 检查玩家燃烧状态变化
        if (isPlayerBurn)
        {
            // 玩家正在接触火物体，重置计时器
            playerBurnTimer = 0f;
            wasPlayerBurning = true;

            // 应用燃烧视觉效果（确保在接触火物体时始终显示燃烧效果）
            if (playerRenderer != null)
            {
                playerRenderer.color = burningTint;
                if (burningMaterial != null && playerRenderer.material != burningMaterial)
                    playerRenderer.material = burningMaterial;

                // 确保火焰粒子效果存在
                if (fireParticleSystemPrefab != null && currentFireParticle == null)
                {
                    currentFireParticle = Instantiate(fireParticleSystemPrefab, transform);
                    
                    // 使用FireParticleAutoSize组件自动调整大小
                    FireParticleAutoSize autoSizer = currentFireParticle.GetComponent<FireParticleAutoSize>();
                    if (autoSizer != null)
                    {
                        autoSizer.AdjustToTarget(playerRenderer);
                    }
                }
            }
        }
        else if (wasPlayerBurning)
        {
            // 玩家不再接触火物体，但之前在燃烧
            playerBurnTimer += Time.deltaTime;

            // 如果超过最大燃烧时间，停止玩家燃烧
            if (playerBurnTimer >= maxPlayerBurnTime)
            {
                wasPlayerBurning = false;

                // 恢复原始材质和颜色
                if (playerRenderer != null)
                {
                    playerRenderer.material = originalMaterial;
                    playerRenderer.color = originalTint;
                }

                // 销毁火焰粒子
                if (currentFireParticle != null)
                {
                    // 使用FireParticleAutoSize组件淡出火焰
                    FireParticleAutoSize autoSizer = currentFireParticle.GetComponent<FireParticleAutoSize>();
                    if (autoSizer != null)
                    {
                        autoSizer.FadeOut(2.0f);
                    }
                    else
                    {
                        Destroy(currentFireParticle, 2.0f);
                    }
                    currentFireParticle = null;
                }
            }
        }

        // 定期造成伤害 - 只在玩家燃烧时造成伤害
        if ((isPlayerBurn || wasPlayerBurning) && elapsedTime >= nextDamageTime)
        {
            // 造成伤害
            GameEvents.TriggerPlayerDamaged(burningDamage);

            // 闪烁效果
            if (playerRenderer != null)
            {
                playerRenderer.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                playerRenderer.color = isPlayerBurn || wasPlayerBurning ? burningTint : originalTint;
            }

            // 设置下一次伤害时间
            nextDamageTime = elapsedTime + burningDamageInterval;
        }

        // 检查是否应该退出循环 - 玩家不再燃烧
        if (!isPlayerBurn && !wasPlayerBurning)
        {
            break;
        }

        yield return null;
    }

    // 如果状态仍然是燃烧，恢复正常状态
    if (currentPlayerState == PlayerState.Burning)
    {
        // 恢复发射绳索能力
        GameEvents.TriggerCanShootRopeChanged(true);

        // 重置玩家燃烧状态
        GameEvents.TriggerSetPlayerBurning(false);

        // 恢复原始材质和颜色
        if (playerRenderer != null)
        {
            playerRenderer.material = originalMaterial;
            playerRenderer.color = originalTint;
        }

        // 销毁火焰粒子
        if (currentFireParticle != null)
        {
            // 使用FireParticleAutoSize组件淡出火焰
            FireParticleAutoSize autoSizer = currentFireParticle.GetComponent<FireParticleAutoSize>();
            if (autoSizer != null)
            {
                autoSizer.FadeOut(2.0f);
            }
            else
            {
                Destroy(currentFireParticle, 2.0f);
            }
            currentFireParticle = null;
        }

        // 检查当前环境，决定下一个状态
        CheckEnvironmentForStateTransition();
    }
}

    // 应用麻痹状态
    private IEnumerator ApplyParalyzedState()
    {
        // 设置电击冷却状态
        isElectrifiedOnCooldown = true;

        // 如果有PlayerController，通知它电击免疫状态
        if (playerController != null)
        {
            playerController.SetElectricImmunity(true);
        }

        // 应用麻痹视觉效果 - 使用淡化的电击材质
        if (playerRenderer != null)
        {
            playerRenderer.color = new Color(electrifiedTint.r, electrifiedTint.g, electrifiedTint.b, 0.7f); // 稍微淡化的电击颜色
            if (electrifiedMaterial != null)
            {
                // 创建材质实例以便修改属性
                Material paralyzedMaterial = new Material(electrifiedMaterial);
                // 如果材质有透明度属性，可以调整它
                if (paralyzedMaterial.HasProperty("_Alpha"))
                {
                    paralyzedMaterial.SetFloat("_Alpha", 0.7f);
                }
                playerRenderer.material = paralyzedMaterial;
            }
        }

        // 启动闪烁效果协程
        if (electrifiedBlinkCoroutine != null)
        {
            StopCoroutine(electrifiedBlinkCoroutine);
        }
        electrifiedBlinkCoroutine = StartCoroutine(ElectrifiedBlinkRoutine());

        // 应用麻痹效果 - 降低移动速度
        float originalMoveSpeed = 0f;
        if (playerController != null)
        {
            originalMoveSpeed = playerController.GetMoveSpeed();
            playerController.SetMoveSpeed(originalMoveSpeed * 0.5f); // 降低为原来的50%
        }

        // 禁用发射绳索能力
        GameEvents.TriggerCanShootRopeChanged(false);

        // 麻痹持续时间
        float paralysisDuration = 3.0f;
        float elapsedTime = 0f;

        Debug.LogFormat("玩家进入麻痹状态，持续 {0} 秒", paralysisDuration);

        // 等待麻痹时间结束
        while (elapsedTime < paralysisDuration && currentPlayerState == PlayerState.Paralyzed)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 麻痹结束，恢复正常状态
        if (currentPlayerState == PlayerState.Paralyzed)
        {
            // 恢复原始移动速度
            if (playerController != null)
            {
                playerController.SetMoveSpeed(originalMoveSpeed);
            }

            // 恢复发射绳索能力
            GameEvents.TriggerCanShootRopeChanged(true);

            // 停止闪烁效果
            if (electrifiedBlinkCoroutine != null)
            {
                StopCoroutine(electrifiedBlinkCoroutine);
                electrifiedBlinkCoroutine = null;
            }

            // 恢复原始材质和颜色
            if (playerRenderer != null)
            {
                playerRenderer.material = originalMaterial;
                playerRenderer.color = originalTint;
            }

            // 冷却结束
            isElectrifiedOnCooldown = false;

            // 如果有PlayerController，取消电击免疫状态
            if (playerController != null)
            {
                playerController.SetElectricImmunity(false);
            }

            // 检查当前环境，决定下一个状态
            CheckEnvironmentForStateTransition();
        }
    }
    #endregion

    #region Rope Effects

    public void HandleHookTagEffect(string tag)
{
    // 保存当前钩中的物体标签
    string currentHookTag = tag;
    
    // 根据标签设置不同的效果
    switch (tag)
    {
        case "Ice":
                // 检查是否在冰冻冷却中，如果是则不触发
                if (!IsFrozenOnCooldown() && !IsInState(GameEvents.PlayerState.Frozen))
                {
#if UNITY_EDITOR
                    Debug.LogFormat("钩中冰面: 玩家被冻结");
#endif
                    // 触发冰冻状态
                    GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Frozen);
            }
            break;
            
        case "Fire":
                // 燃烧状态不需要冷却检查，但避免重复触发
                if (!IsInState(GameEvents.PlayerState.Burning))
                {
#if UNITY_EDITOR
                    Debug.LogFormat("钩中火焰: 绳索逐渐烧断");
#endif

                    // 触发燃烧状态
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Burning);
            }
            break;
            
        case "Elect":
            // 检查是否在电击冷却中，如果是则不触发
            if (!IsElectrifiedOnCooldown() && !IsInState(GameEvents.PlayerState.Electrified))
            {
                #if UNITY_EDITOR
                Debug.LogFormat("钩中带电物体: 玩家被电击");
                #endif
                // 触发电击状态
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Electrified);
            }
            break;
            
        case "Ground":
        case "Hookable":
        default:
                // 只有当前不是摆动状态时才触发
                if (!IsInState(GameEvents.PlayerState.Swinging) &&
                    !IsInState(GameEvents.PlayerState.Frozen) &&
                    !IsInState(GameEvents.PlayerState.Electrified) &&
                    !IsInState(GameEvents.PlayerState.Burning))
                {
                    // 默认摆动状态
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Swinging);
            }
            break;
    }
}

// 处理绳索颜色变化 - 从RopeSystem移动过来
public void ChangeRopeColor(GameEvents.PlayerState state)
{
    if (ropeSystem == null || ropeSystem.GetComponent<LineRenderer>() == null) return;
    
    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
    Material OriginalRopeMat = lineRenderer.material;

        // 处理冰冻状态
        if (state == GameEvents.PlayerState.Frozen)
        {
            // 应用冰冻颜色
            lineRenderer.startColor = frozenTint;
            lineRenderer.endColor = frozenTint;
            if(RopefrozenMaterial != null)
            lineRenderer.material = RopefrozenMaterial;
        }
        else if (state == GameEvents.PlayerState.Burning)
        {
            // 应用燃烧颜色
            lineRenderer.startColor = burningTint;
            lineRenderer.endColor = burningTint;
            if(RopeburningMaterial != null)
            lineRenderer.material = RopeburningMaterial;
        }
        else if (state == GameEvents.PlayerState.Electrified)
        {
            // 应用电击颜色
            lineRenderer.startColor = electrifiedTint;
            lineRenderer.endColor = electrifiedTint;
            if(RopeelectrifiedMaterial != null)
            lineRenderer.material = RopeelectrifiedMaterial;
        }
        else if (state != GameEvents.PlayerState.Frozen &&
                 state != GameEvents.PlayerState.Burning &&
                 state != GameEvents.PlayerState.Electrified)
        {
            // 恢复原始颜色
            lineRenderer.startColor = ropeSystem.getOriginalColorGradiant().colorKeys[0].color;
            lineRenderer.endColor = ropeSystem.getOriginalColorGradiant().colorKeys[1].color;
            lineRenderer.colorGradient = ropeSystem.getOriginalColorGradiant();
        }
}

private IEnumerator BurnRopeEffect(RopeSystem ropeSystem)
{
    // 初始化燃烧效果
    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
    if (lineRenderer == null) yield break;
    
    // 保存原始宽度和颜色
    float originalStartWidth = lineRenderer.startWidth;
    float originalEndWidth = lineRenderer.endWidth;
    Color originalColor = lineRenderer.startColor;
    
    // 获取燃烧起始锚点索引
    int burnAnchorIndex = ropeSystem.GetBurningAnchorIndex();
    if (burnAnchorIndex < 0) burnAnchorIndex = 0;
    
    // 创建火焰粒子效果
    GameObject fireParticle = null;
    FireParticleAutoSize fireAutoSizer = null;
    
    if (fireParticleSystemPrefab != null)
    {
        // 实例化火焰粒子系统
        fireParticle = Instantiate(fireParticleSystemPrefab);
        
        // 获取并配置自动大小调整组件
        fireAutoSizer = fireParticle.GetComponent<FireParticleAutoSize>();
        if (fireAutoSizer != null)
        {
            fireAutoSizer.SetAsRopeFire(originalStartWidth);
        }
    }
    
    // 燃烧进度
    float burnProgress = 0f;
    float burnSpeed = ropeSystem.GetBurnPropagationSpeed();
    float burnThreshold = ropeSystem.GetBurnBreakThreshold();
    
    // 获取锚点列表
    List<Vector2> anchors = ropeSystem.GetAnchors();
    
    // 保存原始宽度曲线
    AnimationCurve originalWidthCurve = null;
    if (lineRenderer.widthCurve != null)
    {
        // 复制原始曲线的所有关键帧
        originalWidthCurve = new AnimationCurve();
        foreach (Keyframe key in lineRenderer.widthCurve.keys)
        {
            originalWidthCurve.AddKey(key);
        }
    }
    else
    {
        // 如果没有原始曲线，创建一个简单的线性曲线
        originalWidthCurve = new AnimationCurve();
        originalWidthCurve.AddKey(0f, originalStartWidth);
        originalWidthCurve.AddKey(1f, originalEndWidth);
    }
    
    while (burnProgress < 1.0f && currentRopeState == RopeState.Burning && ropeSystem.HasAnchors())
    {
        burnProgress += Time.deltaTime * burnSpeed / 10f; // 调整为适当的燃烧速度
        
        // 更新线渲染器颜色和宽度
        if (lineRenderer != null)
        {
            // 从燃烧点开始向两端传播
            int pointCount = lineRenderer.positionCount;
            
            // 计算燃烧点在线渲染器中的索引
            int burnPointIndex = burnAnchorIndex + 1;
            
            // 计算每个点的燃烧程度和颜色
            Color[] colors = new Color[pointCount];
            
            // 定义颜色
            Color fireColor = new Color(1.0f, 0.3f, 0.1f); // 火红色
            Color charColor = new Color(0.1f, 0.1f, 0.1f); // 烧焦的黑色
            
            // 计算影响范围
            float burnRange = 0.3f; // 燃烧影响的范围比例
            float charRange = 0.1f; // 烧焦影响的范围比例（应小于burnRange）
            
            for (int i = 0; i < pointCount; i++)
            {
                // 计算该点到燃烧起始点的归一化距离
                float normalizedDistance;
                
                if (pointCount <= 1)
                {
                    normalizedDistance = 0;
                }
                else
                {
                    float burnPointPos = (float)burnPointIndex / (pointCount - 1);
                    float pointPos = (float)i / (pointCount - 1);
                    normalizedDistance = Mathf.Abs(pointPos - burnPointPos);
                }
                
                // 根据距离和燃烧进度计算颜色
                if (normalizedDistance < charRange * burnProgress)
                {
                    // 最中心的烧焦部分 - 黑色
                    float charIntensity = 1.0f - (normalizedDistance / (charRange * burnProgress));
                    colors[i] = Color.Lerp(fireColor, charColor, charIntensity * burnProgress);
                }
                else if (normalizedDistance < burnRange)
                {
                    // 燃烧部分 - 红色到橙色渐变
                    float fireIntensity = 1.0f - ((normalizedDistance - (charRange * burnProgress)) / (burnRange - (charRange * burnProgress)));
                    fireIntensity = Mathf.Clamp01(fireIntensity * burnProgress);
                    colors[i] = Color.Lerp(originalColor, fireColor, fireIntensity);
                }
                else
                {
                    // 远离燃烧点的部分保持原色
                    colors[i] = originalColor;
                }
            }
            
            // 复制原始宽度曲线
            AnimationCurve widthCurve = new AnimationCurve();
            foreach (Keyframe key in originalWidthCurve.keys)
            {
                widthCurve.AddKey(key);
            }
            
            // 计算燃烧点的归一化位置
            float burnPointPosition = (float)burnPointIndex / (pointCount - 1);
            
            // 燃烧点宽度从原始宽度逐渐变细到0.01
            float minWidth = 0.01f;
            float burnWidth = Mathf.Lerp(originalEndWidth, minWidth, burnProgress * 0.8f);
            
            // 在燃烧点位置添加关键帧
            // 先检查是否已经有接近该位置的关键帧
            bool keyExists = false;
            for (int i = 0; i < widthCurve.keys.Length; i++)
            {
                if (Mathf.Abs(widthCurve.keys[i].time - burnPointPosition) < 0.01f)
                {
                    // 如果已有关键帧，更新它
                    Keyframe existingKey = widthCurve.keys[i];
                    existingKey.value = burnWidth;
                    widthCurve.MoveKey(i, existingKey);
                    keyExists = true;
                    break;
                }
            }
            
            // 如果没有找到接近的关键帧，添加新的
            if (!keyExists)
            {
                int keyIndex = widthCurve.AddKey(burnPointPosition, burnWidth);
                
                // 设置切线为0，使曲线在该点有尖锐的变化
                if (keyIndex >= 0)
                {
                    Keyframe newKey = widthCurve.keys[keyIndex];
                    newKey.inTangent = 0;
                    newKey.outTangent = 0;
                    widthCurve.MoveKey(keyIndex, newKey);
                }
            }
            
            // 应用颜色
            lineRenderer.colorGradient = CreateGradient(colors);
            
            // 应用宽度曲线
            lineRenderer.widthCurve = widthCurve;
            
            // 更新火焰粒子位置
            if (fireParticle != null && burnPointIndex < pointCount)
            {
                // 计算火焰位置 - 在燃烧最严重的部分
                Vector3 firePosition = lineRenderer.GetPosition(burnPointIndex);
                fireParticle.transform.position = firePosition;
                
                // 随着燃烧进度增加火焰强度
                if (fireAutoSizer != null)
                {
                    fireAutoSizer.IncreaseIntensity(burnProgress);
                }
            }
        }
        
        // 检查是否达到断开阈值
        if (burnProgress >= burnThreshold)
        {
            // 从燃烧点断开绳索
            ropeSystem.BreakRopeAtBurningPoint();
            
            // 触发绳索释放事件
            GameEvents.TriggerRopeReleased();
            GameEvents.TriggerCanShootRopeChanged(true);
            
            // 销毁火焰粒子
            if (fireParticle != null)
            {
                // 使用FireParticleAutoSize组件淡出火焰
                if (fireAutoSizer != null)
                {
                    fireAutoSizer.FadeOut(2.0f);
                }
                else
                {
                    Destroy(fireParticle, 2.0f);
                }
            }
            
            break;
        }
        
        yield return null;
    }
    
    // 恢复原始外观
    if (lineRenderer != null && ropeSystem.HasAnchors())
    {
        // 重置宽度 - 使用原始宽度曲线
        lineRenderer.widthCurve = ropeSystem.getOriginalCurve(); // 使用RopeSystem中保存的原始曲线
        lineRenderer.startWidth = originalStartWidth;
        lineRenderer.endWidth = originalEndWidth;
        
        // 恢复原始颜色
        lineRenderer.startColor = originalColor;
        lineRenderer.endColor = originalColor;
        lineRenderer.colorGradient = ropeSystem.getOriginalColorGradiant();
    }
    
    // 销毁火焰粒子
    if (fireParticle != null)
    {
        // 使用FireParticleAutoSize组件淡出火焰
        if (fireAutoSizer != null)
        {
            fireAutoSizer.FadeOut(2.0f);
        }
        else
        {
            Destroy(fireParticle, 2.0f);
        }
    }
}

    // 创建颜色渐变
    private Gradient CreateGradient(Color[] colors)
    {
        Gradient gradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[colors.Length];
        
        for (int i = 0; i < colors.Length; i++)
        {
            float time = (float)i / (colors.Length - 1);
            colorKeys[i] = new GradientColorKey(colors[i], time);
            alphaKeys[i] = new GradientAlphaKey(colors[i].a, time);
        }
        
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }
    #endregion

    #region Visual Effects
    // 电击视觉效果
    private IEnumerator ElectrifiedVisualEffects()
    {
        if (playerRenderer == null) yield break;
        
        // 闪烁颜色
        Color blinkColor1 = new Color(0.2f, 0.2f, 0.2f);
        Color blinkColor2 = electrifiedTint;
        
        // 持续闪烁直到状态改变
        while (currentPlayerState == PlayerState.Electrified)
        {
            // 闪烁效果
            playerRenderer.color = blinkColor1;
            yield return new WaitForSeconds(0.05f);
            
            // 再次检查状态，确保不会在状态改变后设置颜色
            if (currentPlayerState != PlayerState.Electrified) break;
            
            playerRenderer.color = blinkColor2;
            yield return new WaitForSeconds(0.05f);
        }
        
        // 确保在协程结束时不会保留电击颜色（如果状态已经改变）
        if (currentPlayerState != PlayerState.Electrified && 
            currentPlayerState != PlayerState.Paralyzed && 
            playerRenderer != null)
        {
            playerRenderer.color = originalTint;
        }
    }
    
        private IEnumerator ElectrifiedBlinkRoutine()
    {
        if (playerRenderer == null) yield break;
        
        // 闪烁颜色
        Color blinkColor1 = new Color(0.2f, 0.2f, 0.2f);
        Color blinkColor2 = electrifiedTint;
        
        float blinkSpeed = 0.15f; // 闪烁速度
        
        // 持续闪烁直到状态改变
        while (currentPlayerState == PlayerState.Paralyzed)
        {
            // 闪烁效果
            playerRenderer.color = blinkColor1;
            yield return new WaitForSeconds(blinkSpeed);

            // 再次检查状态，确保不会在状态改变后设置颜色
            if (currentPlayerState != PlayerState.Paralyzed) break;

            playerRenderer.color = blinkColor2;
            yield return new WaitForSeconds(blinkSpeed);
        }
        
        // 确保在协程结束时不会保留电击颜色（如果状态已经改变）
        if (currentPlayerState != PlayerState.Paralyzed && 
            playerRenderer != null)
        {
            playerRenderer.color = originalTint;
        }
    }
    #endregion

    #region Cooldown Routines
    // 冰冻冷却
    private IEnumerator FrozenCooldownRoutine()
    {
        // 设置冰冻冷却状态
        isFrozenOnCooldown = true;

        // 如果有PlayerController，通知它冰免疫状态
        if (playerController != null)
        {
            playerController.SetIceImmunity(true);
        }

        // 添加冷却期间的白色闪烁效果
        StartCoroutine(CooldownBlinkEffect(frozenCooldown));

        // 等待冷却时间
        yield return new WaitForSeconds(frozenCooldown);

        // 冷却结束
        isFrozenOnCooldown = false;

        // 如果有PlayerController，取消冰免疫状态
        if (playerController != null)
        {
            playerController.SetIceImmunity(false);
            GameEvents.TriggerCanShootRopeChanged(true);
        }
    }

    // 电击冷却
    private IEnumerator ElectrifiedCooldownRoutine()
    {
        // 设置电击冷却状态
        isElectrifiedOnCooldown = true;

        // 如果有PlayerController，通知它电击免疫状态
        if (playerController != null)
        {
            playerController.SetElectricImmunity(true);
        }

        // 添加冷却期间的白色闪烁效果
        StartCoroutine(CooldownBlinkEffect(electrifiedCooldown));

        // 等待冷却时间
        yield return new WaitForSeconds(electrifiedCooldown);

        // 冷却结束，恢复发射绳索能力
        if (playerController != null)
        {
            GameEvents.TriggerCanShootRopeChanged(true);
        }

        // 恢复原始颜色
        if (playerRenderer != null)
        {
            playerRenderer.color = originalTint;
        }

        // 冷却结束
        isElectrifiedOnCooldown = false;

        // 如果有PlayerController，取消电击免疫状态
        if (playerController != null)
        {
            playerController.SetElectricImmunity(false);
        }
    }

    // 添加通用的冷却闪烁效果
    private IEnumerator CooldownBlinkEffect(float duration)
    {
        if (playerRenderer == null) yield break;
        
        Color originalColor = playerRenderer.color;
        Color blinkColor = Color.white;
        Material originalMat = playerRenderer.material;
        
        float elapsedTime = 0f;
        float blinkInterval = 0.2f; // 闪烁间隔
        bool isWhite = false;
        
        while (elapsedTime < duration)
        {
            // 在白色和免疫颜色之间闪烁
            if (isWhite)
            {
                playerRenderer.color = originalColor;
            }
            else
            {
                playerRenderer.color = blinkColor;
            }
            
            isWhite = !isWhite;
            
            // 等待闪烁间隔
            float waitTime = Mathf.Min(blinkInterval, duration - elapsedTime);
            yield return new WaitForSeconds(waitTime);
            
            elapsedTime += waitTime;
        }
        
        // 确保最后是原始颜色和材质
        playerRenderer.color = originalColor;
        
        // 如果当前状态是正常状态，恢复原始材质
        if (currentPlayerState == PlayerState.Normal)
        {
            playerRenderer.material = originalMat;
        }
    }
    #endregion

    #region Utility Methods
    // 检查当前环境，决定下一个状态
    private void CheckEnvironmentForStateTransition()
    {
        // 获取玩家当前位置
        Vector2 playerPosition = transform.position;

        // 检查玩家是否在火上
        if (isPlayerBurn && !playerController.IsFireImmune())
        {
            SetPlayerState(PlayerState.Burning);
            return;
        }

        // 检查玩家是否在冰上 - 使用OverlapCircle检测
        Collider2D[] iceColliders = Physics2D.OverlapCircleAll(playerPosition, 0.5f);
        foreach (Collider2D collider in iceColliders)
        {
            if (collider.CompareTag("Ice") && !playerController.IsIceImmune() && !isFrozenOnCooldown)
            {
                SetPlayerState(PlayerState.Frozen);
                return;
            }
        }

        // 检查玩家是否在电上 - 使用OverlapCircle检测
        Collider2D[] electColliders = Physics2D.OverlapCircleAll(playerPosition, 0.5f);
        foreach (Collider2D collider in electColliders)
        {
            if (collider.CompareTag("Elect") && !playerController.IsElectricImmune() && !isElectrifiedOnCooldown)
            {
                SetPlayerState(PlayerState.Electrified);
                return;
            }
        }

        SetPlayerState(PlayerState.Normal);
    }

    // 设置玩家燃烧状态
    public void SetPlayerBurn(bool canBurn)
    {
        isPlayerBurn = canBurn;
        
        // 如果玩家开始燃烧，且当前在冰冻状态
        if (canBurn && currentPlayerState == PlayerState.Frozen)
        {
            // 冰冻状态会被火解除，但这个逻辑已经在ApplyFrozenState中处理
            // 这里不需要额外操作，因为ApplyFrozenState会检测isPlayerBurn
        }
        // 如果玩家开始燃烧，且当前不在燃烧状态，且没有火免疫
        else if (canBurn && currentPlayerState != PlayerState.Burning && 
                !playerController.IsFireImmune() && !isFireImmuneAfterFrozen)
        {
            SetPlayerState(PlayerState.Burning);
        }
        // 如果玩家停止燃烧，且当前在燃烧状态
        else if (!canBurn && currentPlayerState == PlayerState.Burning)
        {
            // 不立即改变状态，让燃烧状态协程处理残留燃烧效果
        }
    }
    #endregion

    #region Public Methods
    // 获取当前玩家状态
    public PlayerState GetCurrentPlayerState()
    {
        return currentPlayerState;
    }

    // 获取当前绳索状态
    public RopeState GetCurrentRopeState()
    {
        return currentRopeState;
    }

    // 检查玩家是否处于特定状态
    public bool IsInState(PlayerState state)
    {
        return currentPlayerState == state;
    }

    // 检查绳索是否处于特定状态
    public bool IsRopeInState(RopeState state)
    {
        return currentRopeState == state;
    }

    // 检查是否在冰冻冷却中
    public bool IsFrozenOnCooldown()
    {
        return isFrozenOnCooldown;
    }

    // 检查是否在电击冷却中
    public bool IsElectrifiedOnCooldown()
    {
        return isElectrifiedOnCooldown;
    }

    // 兼容GameEvents.PlayerState的检查方法
    public bool IsInState(GameEvents.PlayerState gameEventState)
    {
        PlayerState state = ConvertGameEventStateToPlayerState(gameEventState);
        return currentPlayerState == state;
    }
    #endregion
}