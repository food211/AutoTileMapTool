using System.Collections;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("动画组件")]
    [SerializeField] private Animator animator;

    [Header("动画状态参数")]
    [SerializeField] private string animStateParam = "AnimState"; // 整数类型参数，用于控制动画状态

    [Header("待机动画设置")]
    [SerializeField] private float idleRandomInterval = 3f;
    [SerializeField] private float idleRandomRange = 2f;
    [SerializeField] private string idleIndexParam = "IdleIndex";
    private bool isTransitioningFromRun = false;

    [Header("状态修正设置")]
    [SerializeField] private float stateCheckInterval = 0.5f; // 状态检查间隔时间
    [SerializeField] private float stuckInAirTimeout = 1.0f; // 卡在空中状态的超时时间
    private float stateCheckTimer = 0f; // 状态检查计时器
    private float stuckInAirTimer = 0f; // 卡在空中计时器

    [Header("瞄准设置")]
    [SerializeField] private float aimIdleTimeoutDuration = 5f; // 瞄准状态超时时间（秒）
    private float aimIdleTimer = 0f; // 瞄准状态计时器
    private bool hasRecentInput = false; // 是否有最近的输入

    [Header("其他动画参数")]
    [SerializeField] private string velocityYParam = "VelocityY"; // Y轴速度参数
    [SerializeField] private string isAimingParam = "IsAiming"; // 是否瞄准参数
    [SerializeField] private string isRopeModeParam = "IsRopeMode"; // 是否处于绳索模式参数

    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody2D playerRb;

    #if UNITY_EDITOR
    [Header("调试设置")]
    [SerializeField] private bool debugMode = false;
    #endif

    // 动画状态枚举 - 简化版
    public enum AnimState
    {
        Idle = 0,
        Run = 1,
        JumpPrepare = 2,
        InAir = 3,
        Land = 4,
        Die = 5,
        AimIdle = 6 // 新增瞄准待机状态
    }

    // 当前状态
    private AnimState currentState = AnimState.Idle;
    private AnimState previousState = AnimState.Idle;

    // 状态控制变量
    private float stateTime = 0f;
    private bool isDead = false;
    private bool isJumpAnimationPlaying = false;
    private bool isLandAnimationPlaying = false;
    private int currentIdleIndex = 0;
    private float idleTimer = 0f;
    private float nextIdleChangeTime;
    private bool isAiming = false; // 是否正在瞄准
    private bool isRopeMode = false; // 是否处于绳索模式

    // 状态缓存
    private bool wasGrounded = true;
    private float lastVelocityY = 0f;
    private bool wasAiming = false; // 上一帧是否瞄准
    private bool wasRopeMode = false; // 上一帧是否处于绳索模式

    // 动画时长缓存（可以根据实际情况手动设置或从动画剪辑中获取）
    [SerializeField]public float jumpPrepareAnimDuration = 0.02f; // 跳跃准备动画时长
    [SerializeField]public float landAnimDuration = 0.02f; // 着陆动画时长

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
        ChangeState(AnimState.Idle);
        SetRandomIdleAnimation();
    }

    private void Update()
    {
        // 如果玩家死亡，不更新动画状态
        if (isDead && currentState != AnimState.Die)
            return;

        // 更新状态计时器
        stateTime += Time.deltaTime;

        // 更新Y轴速度参数
        UpdateVelocityYParameter();

        // 更新绳索模式状态
        UpdateRopeModeState();

        // 更新瞄准状态
        UpdateAimingState();

        // 处理待机动画随机切换
        HandleIdleAnimationSwitching();

        // 确定下一个动画状态
        DetermineNextState();
        // 定期检查和修正动画状态
        CheckAndCorrectAnimationState();
    }

    private void UpdateVelocityYParameter()
    {
        if (animator == null || playerRb == null)
            return;
            
        float velocityY = playerRb.velocity.y;
        
        // 添加阈值判断，将极小值视为0
        if (Mathf.Abs(velocityY) < 0.001f) velocityY = 0f;
        
        // 只在值发生明显变化时更新参数
        if (Mathf.Abs(velocityY - lastVelocityY) > 0.5f)
        {
            animator.SetFloat(velocityYParam, velocityY);
            lastVelocityY = velocityY;
        }
    }

    private void UpdateRopeModeState()
    {
        if (animator == null || playerController == null)
            return;

        // 获取当前绳索模式状态
        isRopeMode = playerController.isPlayerRopeMode();

        // 如果绳索模式状态发生变化，更新动画参数
        if (isRopeMode != wasRopeMode)
        {
            animator.SetBool(isRopeModeParam, isRopeMode);
            wasRopeMode = isRopeMode;

            // 如果进入绳索模式且当前不是InAir状态，则切换到InAir状态
            if (isRopeMode && currentState != AnimState.InAir)
            {
                ChangeState(AnimState.InAir);
            }
            // 如果退出绳索模式，检查是否已着地，决定切换到哪个状态
            else if (!isRopeMode)
            {
                bool isGrounded = playerController.isPlayerGrounded();
                float horizontalSpeed = Mathf.Abs(playerRb.velocity.x);

                if (isGrounded)
                {
                    // 如果已着地且有水平速度，切换到跑步状态
                    if (horizontalSpeed > 0.1f)
                    {
                        ChangeState(AnimState.Run);
                    }
                    // 如果已着地且没有水平速度，根据瞄准状态切换到待机或瞄准待机
                    else
                    {
                        ChangeState(isAiming ? AnimState.AimIdle : AnimState.Idle);
                    }

#if UNITY_EDITOR
                    if (debugMode)
                    {
                        Debug.LogFormat($"退出绳索模式，已着地，切换到{(horizontalSpeed > 0.1f ? "跑步" : isAiming ? "瞄准待机" : "待机")}状态");
                    }
#endif
                }
                else
                {
                    // 如果未着地，保持InAir状态
                    if (currentState != AnimState.InAir)
                    {
                        ChangeState(AnimState.InAir);
                    }

#if UNITY_EDITOR
                    if (debugMode)
                    {
                        Debug.LogFormat($"退出绳索模式，未着地，保持空中状态");
                    }
#endif
                }
            }

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat($"绳索模式状态变化: {isRopeMode}");
            }
#endif
        }
    }

    private void UpdateAimingState()
    {
        if (animator == null || playerController == null)
            return;

        // 获取当前瞄准状态
        bool actualAiming = playerController.isPlayerAiming();

        // 检测玩家输入
        CheckPlayerInput();

        // 如果实际瞄准状态变为false，但当前仍在瞄准待机状态，不立即改变isAiming
        // 而是让HandleAimIdleTimeout方法处理延迟切换
        if (actualAiming)
        {
            // 实际瞄准时，直接设置为瞄准状态并重置计时器
            isAiming = true;
            aimIdleTimer = 0f;
        }
        else if (isAiming && currentState == AnimState.AimIdle)
        {
            // 松开瞄准键但仍在瞄准待机状态，保持isAiming为true
            // 计时器会在HandleAimIdleTimeout中处理
        }
        else
        {
            // 其他情况下，直接跟随实际瞄准状态
            isAiming = actualAiming;
        }

        // 处理瞄准状态超时
        HandleAimIdleTimeout();

        // 只在瞄准状态发生变化时更新参数
        if (isAiming != wasAiming)
        {
            animator.SetBool(isAimingParam, isAiming);
            wasAiming = isAiming;

            // 如果瞄准状态改变，且当前是待机状态，则切换到对应的待机状态
            if (currentState == AnimState.Idle || currentState == AnimState.AimIdle)
            {
                ChangeState(isAiming ? AnimState.AimIdle : AnimState.Idle);
            }

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat($"瞄准状态变化: {isAiming}");
            }
#endif
        }
    }

    // 添加检测玩家输入的方法
    private void CheckPlayerInput()
    {
        // 检测是否有任何相关输入
        bool hasInput = Input.GetAxisRaw("Horizontal") != 0 || 
                        Input.GetAxisRaw("Vertical") != 0 || 
                        Input.GetKeyDown(KeyCode.Z) || 
                        Input.GetKeyDown(KeyCode.X) || 
                        Input.GetKeyDown(KeyCode.Space);

        if (hasInput)
        {
            hasRecentInput = true;
            // 如果有输入，重置瞄准计时器
            if (currentState == AnimState.AimIdle)
            {
                aimIdleTimer = 0f;
            }
        }
        else
        {
            hasRecentInput = false;
        }
    }

    // 添加处理瞄准状态超时的方法
    private void HandleAimIdleTimeout()
    {
        // 如果不在瞄准待机状态，直接重置计时器并返回
        if (currentState != AnimState.AimIdle)
        {
            aimIdleTimer = 0f;
            isTransitioningFromRun = false;
            return;
        }

        // 获取实际瞄准状态
        bool actualAiming = playerController.isPlayerAiming();

        // 如果实际在瞄准，重置计时器并返回
        if (actualAiming)
        {
            aimIdleTimer = 0f;
            isTransitioningFromRun = false;
            return;
        }

        // 如果有最近输入，重置计时器并返回
        if (hasRecentInput)
        {
            aimIdleTimer = 0f;
            return;
        }

        // 在瞄准待机状态下且实际未瞄准且无输入时计时
        // 如果是从跑步过渡来的，使用更短的超时时间
        float timeoutDuration = isTransitioningFromRun ? aimIdleTimeoutDuration * 0.8f : aimIdleTimeoutDuration;
        aimIdleTimer += Time.deltaTime;

        // 如果超过设定时间，切换到普通待机状态
        if (aimIdleTimer >= timeoutDuration)
        {
            // 切换到普通待机状态
            isAiming = false;
            animator.SetBool(isAimingParam, false);
            ChangeState(AnimState.Idle);

            // 设置随机待机动画
            SetRandomIdleAnimation();

            // 重置计时器和过渡标记
            aimIdleTimer = 0f;
            isTransitioningFromRun = false;

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat("瞄准状态超时，切换到普通待机动画");
            }
#endif
        }
    }

    private void DetermineNextState()
    {
        // 如果正在播放一次性动画（跳跃准备、着陆），等待它们完成
        if (IsPlayingOneTimeAnimation())
            return;
            
        bool isGrounded = playerController.isPlayerGrounded();
        float horizontalSpeed = Mathf.Abs(playerRb.velocity.x);
        
        // 检测着地
        if (!wasGrounded && isGrounded && currentState == AnimState.InAir)
        {
            ChangeState(AnimState.Land);
            return;
        }
        
        // 根据当前状态和条件决定下一个状态
        switch (currentState)
        {
            case AnimState.Idle:
                // 从待机切换到其他状态
                if (!isGrounded)
                {
                    ChangeState(AnimState.InAir);
                }
                else if (horizontalSpeed > 0.1f)
                {
                    ChangeState(AnimState.Run);
                }
                else if (isAiming)
                {
                    ChangeState(AnimState.AimIdle);
                }
                break;
                
            case AnimState.AimIdle:
                // 从瞄准待机切换到其他状态
                if (!isGrounded)
                {
                    ChangeState(AnimState.InAir);
                }
                else if (horizontalSpeed > 0.1f)
                {
                    ChangeState(AnimState.Run);
                }
                // 注意：不再在这里检查!isAiming，让超时处理来决定何时切换回普通待机
                break;
                
            case AnimState.Run:
                // 从跑步切换到其他状态
                if (!isGrounded)
                {
                    ChangeState(AnimState.InAir);
                }
                else if (horizontalSpeed < 0.1f)
                {
                    // 跑步结束时，总是先切换到AimIdle状态
                    isTransitioningFromRun = true;
                    ChangeState(AnimState.AimIdle);
                    // 重置计时器，开始倒计时
                    aimIdleTimer = 0f;
                }
                break;
                
            case AnimState.InAir:
                // 从空中状态切换（着陆检测已在前面处理）
                break;
                
            case AnimState.Land:
                // 着陆动画会通过OnLandComplete自动切换到下一个状态
                break;
        }
        
        // 更新接地状态缓存
        wasGrounded = isGrounded;
    }

    private void CheckAndCorrectAnimationState()
    {
        // 如果玩家死亡或正在播放一次性动画，不进行修正
        if (isDead || IsPlayingOneTimeAnimation() || isRopeMode)
        {
            stateCheckTimer = 0f;
            stuckInAirTimer = 0f;
            return;
        }

        // 增加计时器
        stateCheckTimer += Time.deltaTime;

        // 只在设定的间隔时间执行检查
        if (stateCheckTimer < stateCheckInterval)
            return;

        // 重置计时器
        stateCheckTimer = 0f;

        // 检查其他可能的状态异常
        bool isGrounded = playerController.isPlayerGrounded();
        float horizontalSpeed = Mathf.Abs(playerRb.velocity.x);

        // 检查是否卡在空中状态
        if (currentState == AnimState.InAir)
        {
            float velocityY = playerRb.velocity.y;

            // 如果已着地但仍在空中状态，或者Y轴速度接近0但持续在空中状态
            if (isGrounded || (Mathf.Abs(velocityY) < 0.1f && !wasGrounded))
            {
                // 增加卡住计时
                stuckInAirTimer += stateCheckInterval;

                // 如果超过设定时间，强制修正状态
                if (stuckInAirTimer >= stuckInAirTimeout)
                {
#if UNITY_EDITOR
                    if (debugMode)
                    {
                        Debug.LogFormat("检测到动画状态异常：卡在空中状态但已着地或静止，强制修正");
                    }
#endif

                    // 强制触发着陆动画
                    ChangeState(AnimState.Land);

                    // 重置计时器
                    stuckInAirTimer = 0f;
                }
            }
            else
            {
                // 如果不满足卡住条件，重置计时器
                stuckInAirTimer = 0f;
            }
        }
        else
        {
            // 不在空中状态，重置计时器
            stuckInAirTimer = 0f;
        }

        // 如果在地面上但显示为空中状态（且不是刚刚落地）
        if (isGrounded && currentState == AnimState.InAir && stateTime > 0.5f)
        {
#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat("检测到动画状态异常：在地面但显示为空中状态，强制修正");
            }
#endif

            // 根据水平速度决定切换到哪个状态
            if (horizontalSpeed > 0.1f)
            {
                ChangeState(AnimState.Run);
            }
            else
            {
                ChangeState(isAiming ? AnimState.AimIdle : AnimState.Idle);
            }
        }
    }

    private bool IsPlayingOneTimeAnimation()
    {
        return isJumpAnimationPlaying || isLandAnimationPlaying;
    }

    private void ChangeState(AnimState newState)
    {
        // 如果状态没变，不做处理
        if (newState == currentState)
            return;
            
        previousState = currentState;
        currentState = newState;
        
        // 重置状态计时器
        stateTime = 0f;
        
        // 设置动画状态参数
        animator.SetInteger(animStateParam, (int)newState);
        
        // 根据新状态执行特定逻辑
        switch (newState)
        {
            case AnimState.Idle:
                // 进入待机状态
                SetRandomIdleAnimation();
                break;
                
            case AnimState.AimIdle:
                // 进入瞄准待机状态
                // 瞄准时不需要随机待机动画
                idleTimer = 0f;
                break;
                
            case AnimState.JumpPrepare:
                // 开始跳跃准备动画
                isJumpAnimationPlaying = true;
                StartCoroutine(WaitForJumpPrepareAnimation());
                break;
                
            case AnimState.Land:
                // 开始着陆动画
                isLandAnimationPlaying = true;
                // 通知PlayerController着陆动画已开始
                StartCoroutine(WaitForLandAnimation());
                break;
                
            case AnimState.Die:
                // 进入死亡状态
                isDead = true;
                break;
        }
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat($"动画状态变化: {previousState} -> {currentState}");
        }
        #endif
    }

    private IEnumerator WaitForJumpPrepareAnimation()
    {
        // 等待跳跃准备动画播放完成
        yield return new WaitForSeconds(jumpPrepareAnimDuration);

        try
        {
            // 跳跃准备完成
            isJumpAnimationPlaying = false;

            // 通知PlayerController跳跃动画已完成
            if (playerController != null)
            {
                playerController.OnJumpAnimationComplete();

                #if UNITY_EDITOR
                if (debugMode)
                {
                    Debug.LogFormat("跳跃准备动作播放完成，已通知PlayerController");
                }
                #endif
            }
            else
            {
                Debug.LogError("PlayerController为空，无法通知跳跃动画完成");
            }

            // 自动切换到空中状态
            ChangeState(AnimState.InAir);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"跳跃动画协程发生错误: {e.Message}");

            // 确保状态被重置
            isJumpAnimationPlaying = false;

            // 尝试通知PlayerController
            if (playerController != null)
            {
                playerController.OnJumpAnimationComplete();
            }

            // 强制切换到空中状态
            ChangeState(AnimState.InAir);
        }
    }

    private IEnumerator WaitForLandAnimation()
    {
        // 等待着陆动画播放完成
        yield return new WaitForSeconds(landAnimDuration);
        
        // 着陆动画完成
        isLandAnimationPlaying = false;
        
        ChangeState(AnimState.Run);
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("着陆动画播放完成");
        }
        #endif
    }

    #region 待机动画管理

    private void HandleIdleAnimationSwitching()
    {
        // 只在普通待机状态下处理随机切换，瞄准待机不需要随机切换
        if (currentState != AnimState.Idle || isDead)
        {
            idleTimer = 0f;
            return;
        }
        
        idleTimer += Time.deltaTime;
        
        if (idleTimer >= nextIdleChangeTime)
        {
            SetRandomIdleAnimation();
            idleTimer = 0f;
            SetNextIdleChangeTime();
        }
    }

    private void SetRandomIdleAnimation()
    {
        if (animator == null || currentState != AnimState.Idle)
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

    #region 公共接口

    /// <summary>
    /// 触发跳跃动画
    /// </summary>
    public void TriggerJumpAnimation()
    {
        // 增加额外的安全检查，确保不会在已经播放动画时再次触发
        if (isDead || isJumpAnimationPlaying || isLandAnimationPlaying)
        {
            #if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogWarning($"无法触发跳跃动画: isDead={isDead}, isJumpAnimationPlaying={isJumpAnimationPlaying}, isLandAnimationPlaying={isLandAnimationPlaying}");
            }
            #endif
            return;
        }
        
        // 停止可能存在的之前的跳跃协程
        StopAllCoroutines();
        
        ChangeState(AnimState.JumpPrepare);
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("触发跳跃准备动画");
        }
        #endif
    }

    /// <summary>
    /// 触发死亡动画
    /// </summary>
    public void TriggerDeathAnimation()
    {
        if (!isDead)
        {
            ChangeState(AnimState.Die);
            
            #if UNITY_EDITOR
            if (debugMode)
            {
                Debug.LogFormat("触发死亡动画");
            }
            #endif
        }
    }

    /// <summary>
    /// 复活玩家
    /// </summary>
    public void Respawn()
    {
        isDead = false;
        ChangeState(isAiming ? AnimState.AimIdle : AnimState.Idle);
        
        #if UNITY_EDITOR
        if (debugMode)
        {
            Debug.LogFormat("玩家复活");
        }
        #endif
    }

    /// <summary>
    /// 获取当前动画状态
    /// </summary>
    public AnimState GetCurrentAnimState()
    {
        return currentState;
    }

    /// <summary>
    /// 检查是否正在播放跳跃或着陆动画
    /// </summary>
    public bool IsPlayingJumpOrLandAnimation()
    {
        return isJumpAnimationPlaying || isLandAnimationPlaying;
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

    #endregion

    #region 事件响应方法

    /// <summary>
    /// 响应玩家死亡事件
    /// </summary>
    private void OnPlayerDied()
    {
        TriggerDeathAnimation();
    }

    /// <summary>
    /// 响应玩家复活事件
    /// </summary>
    private void OnPlayerRespawn()
    {
        Respawn();
    }

    #endregion

    #region 事件订阅/取消订阅

    private void OnEnable()
    {
        // 订阅玩家相关事件
        GameEvents.OnPlayerDied += OnPlayerDied;
        GameEvents.OnPlayerRespawn += OnPlayerRespawn;
    }

    private void OnDisable()
    {
        // 取消订阅玩家相关事件
        GameEvents.OnPlayerDied -= OnPlayerDied;
        GameEvents.OnPlayerRespawn -= OnPlayerRespawn;
        
        // 停止所有协程
        StopAllCoroutines();
    }

    #endregion

    #region 调试方法

    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!debugMode)
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        GUILayout.Label($"当前动画状态: {currentState}");
        GUILayout.Label($"上一个动画状态: {previousState}");
        GUILayout.Label($"状态持续时间: {stateTime:F2}s");
        GUILayout.Label($"是否着地: {playerController?.isPlayerGrounded()}");
        GUILayout.Label($"Y轴速度: {lastVelocityY:F2}");
        GUILayout.Label($"是否瞄准: {isAiming}");
        GUILayout.Label($"瞄准计时器: {aimIdleTimer:F1}s / {aimIdleTimeoutDuration:F1}s"); // 添加这一行
        GUILayout.Label($"有最近输入: {hasRecentInput}"); // 添加这一行
        GUILayout.Label($"当前待机动画: {currentIdleIndex + 1}");
        GUILayout.Label($"待机计时器: {idleTimer:F1}s");
        GUILayout.Label($"下次切换时间: {nextIdleChangeTime:F1}s");
        GUILayout.Label($"是否死亡: {isDead}");
        GUILayout.Label($"正在播放跳跃动画: {isJumpAnimationPlaying}");
        GUILayout.Label($"正在播放着陆动画: {isLandAnimationPlaying}");

        GUILayout.Space(10);
        
        if (GUILayout.Button("触发跳跃动画"))
        {
            TriggerJumpAnimation();
        }
        
        if (GUILayout.Button("触发死亡动画"))
        {
            TriggerDeathAnimation();
        }
        
        if (GUILayout.Button("复活"))
        {
            Respawn();
        }
        
        if (GUILayout.Button("随机切换待机动画"))
        {
            SetRandomIdleAnimation();
        }
        
        if (GUILayout.Button(debugMode ? "关闭调试模式" : "开启调试模式"))
        {
            debugMode = !debugMode;
        }
        
        GUILayout.EndArea();
    }
    #endif

    #endregion
}