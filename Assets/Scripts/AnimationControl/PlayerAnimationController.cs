using System.Collections;
using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
[Header("动画组件")]
[SerializeField] private Animator animator;

[Header("待机动画设置")]
[SerializeField] private float idleRandomInterval = 3f; // 随机切换待机动画的间隔
[SerializeField] private float idleRandomRange = 2f; // 随机时间范围 (±)

[Header("动画参数名称")]
[SerializeField] private string speedParam = "Speed";
[SerializeField] private string isGroundedParam = "IsGrounded";
[SerializeField] private string isRopeModeParam = "IsRopeMode";
[SerializeField] private string isAimingParam = "IsAiming"; // 是否瞄准
// [SerializeField] private string isJumpingParam = "IsJumping"; // 跳跃开始触发器
[SerializeField] private string jumpTrigger = "Jump";
[SerializeField] private string landTrigger = "Land";
[SerializeField] private string idleIndexParam = "IdleIndex";
[SerializeField] private string velocityYParam = "VelocityY";

[Header("状态动画参数")]
[SerializeField] private string playerStateParam = "PlayerState"; // 玩家状态参数
[SerializeField] private string ropeStateParam = "RopeState"; // 绳索状态参数
[SerializeField] private string damagedTrigger = "Damaged"; // 受伤触发器
[SerializeField] private string dieTrigger = "Die"; // 死亡触发器
[SerializeField] private string respawnTrigger = "Respawn"; // 复活触发器

[Header("引用")]
[SerializeField] private PlayerController playerController;
[SerializeField] private Rigidbody2D playerRb;

#if UNITY_EDITOR
[Header("调试设置")]
[SerializeField] private bool debugMode = false; // 调试模式开关
#endif

// 内部变量
private float idleTimer = 0f;
private float nextIdleChangeTime;
private bool wasGrounded = true;
private bool wasInRopeMode = false;
private int currentIdleIndex = 0;

// 动画状态缓存
private float lastSpeed = 0f;
private bool lastGroundedState = true;
private bool lastRopeModeState = false;
private float lastVelocityY = 0f;
private GameEvents.PlayerState lastPlayerState = GameEvents.PlayerState.Normal;
private GameEvents.RopeState lastRopeState = GameEvents.RopeState.Normal;

// 性能优化
private float animationUpdateInterval = 0.1f; // 动画更新间隔
private float lastAnimationUpdateTime = 0f;

// 状态动画控制
private bool isDead = false;
private bool isInSpecialState = false; // 是否处于特殊状态（冰冻、燃烧、电击等）

// 跳跃动画控制
private bool isJumpAnimationPlaying = false; // 是否正在播放跳跃动画
private bool isPlayingLandAnimation = false; // 是否正在播放着陆动画

#region Unity生命周期

private void Awake()
{
    // 自动获取组件
    if (animator == null)
        animator = GetComponent<Animator>();
        
    if (playerController == null)
        playerController = GetComponent<PlayerController>();
        
    if (playerRb == null)
        playerRb = GetComponent<Rigidbody2D>();
        
    // 初始化随机待机时间
    SetNextIdleChangeTime();
}

private void Start()
{
    // 设置初始动画状态
    if (animator != null)
    {
        animator.SetFloat(speedParam, 0f);
        animator.SetBool(isGroundedParam, true);
        animator.SetBool(isRopeModeParam, false);
        animator.SetBool(isAimingParam, false);
        animator.SetFloat(velocityYParam, 0f);
        animator.SetInteger(playerStateParam, (int)GameEvents.PlayerState.Normal);
        animator.SetInteger(ropeStateParam, (int)GameEvents.RopeState.Normal);
        SetRandomIdleAnimation();
    }
}

private void Update()
{
    // 如果玩家死亡，不更新动画参数
    if (isDead) return;
    
    // 性能优化：减少动画更新频率
    if (Time.time >= lastAnimationUpdateTime + animationUpdateInterval)
    {
        UpdateAnimationParameters();
        lastAnimationUpdateTime = Time.time;
    }
    
    // 处理待机动画随机切换
    HandleIdleAnimationSwitching();
    
    // 检测状态变化并触发相应动画
    CheckStateChanges();
}

#endregion

#region 动画参数更新

private void UpdateAnimationParameters()
{
    if (animator == null || playerController == null || playerRb == null)
        return;
        
    // 获取当前状态
    float currentSpeed = Mathf.Abs(playerRb.velocity.x);
    bool isGrounded = playerController.isPlayerGrounded();
    bool isRopeMode = playerController.isPlayerRopeMode();
    bool isAiming = playerController.isPlayerAiming(); // 假设有一个方法检查是否瞄准
    float velocityY = playerRb.velocity.y;
    
    // 添加阈值判断，将极小值视为0
    if (Mathf.Abs(currentSpeed) < 0.001f) currentSpeed = 0f;
    if (Mathf.Abs(velocityY) < 0.001f) velocityY = 0f;
    
    // 只在值发生变化时更新参数，减少不必要的调用
    if (Mathf.Abs(currentSpeed - lastSpeed) > 0.1f)
    {
        animator.SetFloat(speedParam, currentSpeed);
        lastSpeed = currentSpeed;
    }
    
    if (isGrounded != lastGroundedState)
    {
        animator.SetBool(isGroundedParam, isGrounded);
        lastGroundedState = isGrounded;
    }
    
    if (isRopeMode != lastRopeModeState)
    {
        animator.SetBool(isRopeModeParam, isRopeMode);
        lastRopeModeState = isRopeMode;
    }
    
    // 更新瞄准状态
    animator.SetBool(isAimingParam, isAiming);
    
    if (Mathf.Abs(velocityY - lastVelocityY) > 0.5f)
    {
        animator.SetFloat(velocityYParam, velocityY);
        lastVelocityY = velocityY;
    }
}

#endregion

#region 状态变化检测

private void CheckStateChanges()
{
    if (playerController == null)
        return;
        
    bool isGrounded = playerController.isPlayerGrounded();
    bool isRopeMode = playerController.isPlayerRopeMode();
    
    // 检测着地
    if (!wasGrounded && isGrounded && !isPlayingLandAnimation)
    {
        TriggerLandAnimation();
    }
    
    // 更新状态缓存
    wasGrounded = isGrounded;
    wasInRopeMode = isRopeMode;
}

#endregion

#region 待机动画管理

private void HandleIdleAnimationSwitching()
{
    if (playerController == null || !playerController.isPlayerGrounded() || isInSpecialState || 
        isJumpAnimationPlaying || isPlayingLandAnimation)
    {
        idleTimer = 0f;
        return;
    }
    
    // 只在真正待机时（速度很小）才计时
    float currentSpeed = playerRb != null ? Mathf.Abs(playerRb.velocity.x) : 0f;
    if (currentSpeed < 0.1f && !playerController.isPlayerRopeMode())
    {
        idleTimer += Time.deltaTime;
        
        if (idleTimer >= nextIdleChangeTime)
        {
            SetRandomIdleAnimation();
            idleTimer = 0f;
            SetNextIdleChangeTime();
        }
    }
    else
    {
        // 如果玩家移动或进入绳索模式，重置计时器
        idleTimer = 0f;
    }
}

private void SetRandomIdleAnimation()
{
    if (animator == null || isInSpecialState || isJumpAnimationPlaying || isPlayingLandAnimation)
        return;
        
    // 确保不重复播放同一个待机动画
    int newIndex;
    do {
        newIndex = Random.Range(0, 3); // 0, 1, 2 对应三个待机动画
    } while (newIndex == currentIdleIndex);
    
    currentIdleIndex = newIndex;
    animator.SetFloat(idleIndexParam, currentIdleIndex);
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat($"切换到待机动画 {currentIdleIndex + 1}");
    }
#endif
}

private void SetNextIdleChangeTime()
{
    nextIdleChangeTime = idleRandomInterval + Random.Range(-idleRandomRange, idleRandomRange);
    nextIdleChangeTime = Mathf.Max(nextIdleChangeTime, 1f); // 确保最小间隔为1秒
}

    #endregion

    #region 动画触发方法

    public void TriggerJumpAnimation()
    {
        if (animator != null && !isDead && !isInSpecialState && !isJumpAnimationPlaying)
        {
            // 先重置trigger，防止堆叠
            animator.ResetTrigger(jumpTrigger);
            isJumpAnimationPlaying = true;
            animator.SetTrigger(jumpTrigger);

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat("AnimationController收到OnPlayerJump事件，触发SetTrigger: {0}", jumpTrigger);
            }
#endif
        }
    }

    public void TriggerLandAnimation()
    {
        if (animator != null && !isDead && !isPlayingLandAnimation)
        {
            isPlayingLandAnimation = true;
            animator.ResetTrigger(landTrigger); // 重置触发器，防止堆叠
            animator.SetTrigger(landTrigger);

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat("触发落地动画");
            }
#endif
        }
    }

#endregion

#region 事件响应方法

/// <summary>
/// 响应玩家状态变化事件
/// </summary>
private void HandlePlayerStateChanged(GameEvents.PlayerState newState)
{
    if (animator == null) return;
    
    lastPlayerState = newState;
    animator.SetInteger(playerStateParam, (int)newState);
    
    // 根据状态设置特殊状态标志
    isInSpecialState = newState != GameEvents.PlayerState.Normal && newState != GameEvents.PlayerState.Swinging;
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat($"玩家状态变化: {newState}");
    }
#endif
    
    // 根据不同状态执行特殊逻辑
    switch (newState)
    {
        case GameEvents.PlayerState.Frozen:
            HandleFrozenState();
            break;
        case GameEvents.PlayerState.Burning:
            HandleBurningState();
            break;
        case GameEvents.PlayerState.Electrified:
            HandleElectrifiedState();
            break;
        case GameEvents.PlayerState.Normal:
            HandleNormalState();
            break;
    }
}

/// <summary>
/// 响应绳索状态变化事件
/// </summary>
private void HandleRopeStateChanged(GameEvents.RopeState newState)
{
    if (animator == null) return;
    
    lastRopeState = newState;
    animator.SetInteger(ropeStateParam, (int)newState);
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat($"绳索状态变化: {newState}");
    }
#endif
}

    /// <summary>
    /// 响应玩家着地状态变化事件
    /// </summary>
    private void OnPlayerGroundedStateChanged(bool isGrounded)
    {
        if (isGrounded && !isPlayingLandAnimation)
        {
            TriggerLandAnimation();
        }
    }

    /// <summary>
    /// 由Animation Event调用 - 跳跃动画完成
    /// </summary>
    /// 
    public void OnJumpStart()
    {
        isJumpAnimationPlaying = true;

#if UNITY_EDITOR
        if (debugMode)
        {
            Debug.Log("跳跃准备动作开始");
        }
#endif
    }

    public void OnJumpComplete()
    {
        isJumpAnimationPlaying = false;

        // 通知PlayerController跳跃动画已完成
        if (playerController != null)
        {
            playerController.OnJumpAnimationComplete();
        }

#if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("跳跃准备动作播放完成，通知PlayerController");
        }
#endif
    }


/// <summary>
    /// 响应玩家受伤事件
    /// </summary>
    private void HandlePlayerDamaged(int damage)
    {
        if (animator != null && !isDead)
        {
            animator.SetTrigger(damagedTrigger);
#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat($"玩家受伤: {damage}点伤害");
            }
#endif
        }
    }

/// <summary>
/// 响应玩家死亡事件
/// </summary>
private void OnPlayerDied()
{
    if (animator == null) return;
    
    isDead = true;
    isInSpecialState = true;
    animator.ResetTrigger(dieTrigger); // 确保跳跃触发器被重置
    animator.SetTrigger(dieTrigger);

#if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("玩家死亡动画触发");
        }
#endif
}

/// <summary>
/// 响应玩家复活事件
/// </summary>
private void OnPlayerRespawn()
{
    if (animator == null) return;

    animator.ResetTrigger(respawnTrigger); // 确保复活触发器被重置
    animator.SetTrigger(respawnTrigger);

    
#if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("玩家复活动画触发");
        }
#endif
}

/// <summary>
/// 响应玩家复活完成事件
/// </summary>
private void HandlePlayerRespawnCompleted()
{
    isDead = false;
    isInSpecialState = false;
    isJumpAnimationPlaying = false;
    isPlayingLandAnimation = false;
    
    // 重置动画参数
    ResetAnimationParameters();
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat("玩家复活完成，动画系统重置");
    }
#endif
}

#endregion

#region 状态处理方法

private void HandleFrozenState()
{
    // 冰冻状态：停止待机动画切换，可能播放冰冻动画
    idleTimer = 0f;
    if (animator != null)
    {
        animator.SetFloat(speedParam, 0f);
    }
}

private void HandleBurningState()
{
    // 燃烧状态：可能播放燃烧动画效果
    idleTimer = 0f;
}

private void HandleElectrifiedState()
{
    // 电击状态：可能播放电击动画效果
    idleTimer = 0f;
}

private void HandleNormalState()
{
    // 恢复正常状态：重新开始待机动画切换
    isInSpecialState = false;
    SetNextIdleChangeTime();
}

#endregion

#region 公共接口

/// <summary>
/// 手动设置动画参数
/// </summary>
public void SetAnimationParameter(string paramName, float value)
{
    if (animator != null)
    {
        animator.SetFloat(paramName, value);
    }
}

public void SetAnimationParameter(string paramName, bool value)
{
    if (animator != null)
    {
        animator.SetBool(paramName, value);
    }
}

public void SetAnimationParameter(string paramName, int value)
{
    if (animator != null)
    {
        animator.SetInteger(paramName, value);
    }
}

public void TriggerAnimation(string triggerName)
{
    if (animator != null)
    {
        animator.SetTrigger(triggerName);
    }
}

/// <summary>
/// 获取当前动画状态信息
/// </summary>
public bool IsAnimationPlaying(string animationName)
{
    if (animator == null)
        return false;
        
    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    return stateInfo.IsName(animationName);
}

public float GetAnimationProgress()
{
    if (animator == null)
        return 0f;
        
    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    return stateInfo.normalizedTime;
}

/// <summary>
/// 设置动画播放速度
/// </summary>
public void SetAnimationSpeed(float speed)
{
    if (animator != null)
    {
        animator.speed = speed;
    }
}

/// <summary>
/// 重置所有动画参数
/// </summary>
public void ResetAnimationParameters()
{
    if (animator == null)
        return;
        
    animator.SetFloat(speedParam, 0f);
    animator.SetBool(isGroundedParam, true);
    animator.SetBool(isRopeModeParam, false);
    animator.SetBool(isAimingParam, false);
    animator.SetFloat(velocityYParam, 0f);
    animator.SetFloat(idleIndexParam, 0);
    animator.SetInteger(playerStateParam, (int)GameEvents.PlayerState.Normal);
    animator.SetInteger(ropeStateParam, (int)GameEvents.RopeState.Normal);
    animator.ResetTrigger(jumpTrigger);
    animator.ResetTrigger(landTrigger);
    animator.ResetTrigger(damagedTrigger);
    animator.ResetTrigger(dieTrigger);
    animator.ResetTrigger(respawnTrigger);
    
    currentIdleIndex = 0;
    idleTimer = 0f;
    lastPlayerState = GameEvents.PlayerState.Normal;
    lastRopeState = GameEvents.RopeState.Normal;
    isJumpAnimationPlaying = false;
    isPlayingLandAnimation = false;
    SetNextIdleChangeTime();
}

/// <summary>
/// 获取当前玩家状态
/// </summary>
public GameEvents.PlayerState GetCurrentPlayerState()
{
    return lastPlayerState;
}

/// <summary>
/// 获取当前绳索状态
/// </summary>
public GameEvents.RopeState GetCurrentRopeState()
{
    return lastRopeState;
}

/// <summary>
/// 检查是否处于特殊状态
/// </summary>
public bool IsInSpecialState()
{
    return isInSpecialState;
}

/// <summary>
/// 检查是否死亡
/// </summary>
public bool IsDead()
{
    return isDead;
}

/// <summary>
/// 检查是否正在播放跳跃或着陆动画
/// </summary>
public bool IsPlayingJumpOrLandAnimation()
{
    return isJumpAnimationPlaying || isPlayingLandAnimation;
}

/// <summary>
/// 由Animation Event调用 - 着陆动画开始
/// </summary>
public void OnLandStart()
{
    isPlayingLandAnimation = true;
    
    // 通知PlayerController着陆动画已开始
    if (playerController != null)
    {
        playerController.OnLandAnimationStart();
    }
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat("开始播放着陆动画");
    }
#endif
}

/// <summary>
/// 由Animation Event调用 - 着陆动画完成
/// </summary>
public void OnLandComplete()
{
    isPlayingLandAnimation = false;
    
    // 通知PlayerController着陆动画已完成
    if (playerController != null)
    {
        playerController.OnLandAnimationComplete();
    }
    
#if UNITY_EDITOR
    if (debugMode)
    {
        Debug.LogFormat("着陆动画播放完成");
    }
#endif
}

#endregion

#region 事件订阅/取消订阅

private void OnEnable()
{
    // 订阅玩家相关事件
    GameEvents.OnPlayerStateChanged += HandlePlayerStateChanged;
    GameEvents.OnPlayerGroundedStateChanged += OnPlayerGroundedStateChanged;
    GameEvents.OnPlayerDied += OnPlayerDied;
    GameEvents.OnPlayerRespawn += OnPlayerRespawn;
    GameEvents.OnPlayerRespawnCompleted += HandlePlayerRespawnCompleted;
    
    // 订阅绳索相关事件
    GameEvents.OnRopeStateChanged += HandleRopeStateChanged;
}

private void OnDisable()
{
    // 取消订阅玩家相关事件
    GameEvents.OnPlayerStateChanged -= HandlePlayerStateChanged;
    GameEvents.OnPlayerGroundedStateChanged -= OnPlayerGroundedStateChanged;
    GameEvents.OnPlayerDied -= OnPlayerDied;
    GameEvents.OnPlayerRespawn -= OnPlayerRespawn;
    GameEvents.OnPlayerRespawnCompleted -= HandlePlayerRespawnCompleted;
    
    // 取消订阅绳索相关事件
    GameEvents.OnRopeStateChanged -= HandleRopeStateChanged;
    
    // 停止所有协程
    StopAllCoroutines();
}

#endregion

#region 调试方法

#if UNITY_EDITOR
private void OnGUI()
{
    if (!debugMode || animator == null)
        return;
        
    // 获取当前动画状态信息
    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    string currentAnimName = GetCurrentAnimationName(stateInfo);
    float normalizedTime = stateInfo.normalizedTime % 1; // 循环动画的进度(0-1)
    
    GUILayout.BeginArea(new Rect(10, 10, 300, 300));
    GUILayout.Label($"正在播放动画: {currentAnimName}");
    GUILayout.Label($"动画进度: {normalizedTime:P0}"); // 显示为百分比
    GUILayout.Label($"当前速度: {lastSpeed:F2}");
    GUILayout.Label($"是否着地: {lastGroundedState}");
    GUILayout.Label($"绳索模式: {lastRopeModeState}");
    GUILayout.Label($"Y轴速度: {lastVelocityY:F2}");
    GUILayout.Label($"当前待机动画: {currentIdleIndex + 1}");
    GUILayout.Label($"待机计时器: {idleTimer:F1}s");
    GUILayout.Label($"下次切换时间: {nextIdleChangeTime:F1}s");
    GUILayout.Label($"玩家状态: {lastPlayerState}");
    GUILayout.Label($"绳索状态: {lastRopeState}");
    GUILayout.Label($"是否死亡: {isDead}");
    GUILayout.Label($"特殊状态: {isInSpecialState}");
    GUILayout.Label($"正在播放跳跃动画: {isJumpAnimationPlaying}");
    GUILayout.Label($"正在播放着陆动画: {isPlayingLandAnimation}");
    GUILayout.Label($"调试模式: {debugMode}");
    
    // 显示动画触发器状态
    GUILayout.Label("动画触发器状态:");
    GUILayout.Label($"  jumpTrigger: {IsTriggered(jumpTrigger)}");
    GUILayout.Label($"  landTrigger: {IsTriggered(landTrigger)}");
    GUILayout.Label($"  damagedTrigger: {IsTriggered(damagedTrigger)}");
    GUILayout.Label($"  dieTrigger: {IsTriggered(dieTrigger)}");
    GUILayout.Label($"  respawnTrigger: {IsTriggered(respawnTrigger)}");
    
    GUILayout.Space(10);
    
    if (GUILayout.Button("随机切换待机动画"))
    {
        SetRandomIdleAnimation();
    }
    
    if (GUILayout.Button("触发着陆动画"))
    {
        TriggerLandAnimation();
    }
    
    if (GUILayout.Button("触发受伤动画"))
    {
        HandlePlayerDamaged(1);
    }
    
    if (GUILayout.Button(debugMode ? "关闭调试模式" : "开启调试模式"))
    {
        debugMode = !debugMode;
    }
    
    GUILayout.EndArea();
}

// 获取当前播放的动画名称
private string GetCurrentAnimationName(AnimatorStateInfo stateInfo)
{
    // 检查常见的动画状态
    if (stateInfo.IsName("Idle_Random")) return "Idle_Random";
    if (stateInfo.IsName("Player_Anim_Idle_0")) return "Player_Anim_Idle_0";
    if (stateInfo.IsName("Player_Anim_Idle_1")) return "Player_Anim_Idle_1";
    if (stateInfo.IsName("Player_Anim_Idle_2")) return "Player_Anim_Idle_2";
    if (stateInfo.IsName("Player_Anim_Run")) return "Player_Anim_Run";
    if (stateInfo.IsName("Player_Anim_OnJump")) return "Player_Anim_OnJump";
    if (stateInfo.IsName("Player_Anim_InAir")) return "Player_Anim_InAir";
    if (stateInfo.IsName("Player_Anim_OnLand")) return "Player_Anim_OnLand";
    if (stateInfo.IsName("Player_Anim_AimIdle")) return "Player_Anim_AimIdle";
    if (stateInfo.IsName("Player_Anim_Frozen")) return "Player_Anim_Frozen";
    if (stateInfo.IsName("Player_Anim_Burning")) return "Player_Anim_Burning";
    if (stateInfo.IsName("Player_Anim_Electrified")) return "Player_Anim_Electrified";
    if (stateInfo.IsName("Player_Anim_Die")) return "Player_Anim_Die";
    if (stateInfo.IsName("Player_Anim_Respawn")) return "Player_Anim_Respawn";
    
    // 如果是其他动画，返回哈希码
    return $"未知动画({stateInfo.fullPathHash})";
}

// 检查触发器是否被激活
private bool IsTriggered(string triggerName)
{
    // 由于Unity不提供直接方法检查触发器状态，我们只能通过间接方式
    // 这个方法不是100%准确，但可以用于调试
    AnimatorControllerParameter[] parameters = animator.parameters;
    foreach (var param in parameters)
    {
        if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
        {
            // 尝试获取触发器参数的值
            // 注意：这种方法不总是可靠，因为触发器在激活后会被自动重置
            return animator.GetBool(triggerName);
        }
    }
    return false;
}
#endif

#endregion
}