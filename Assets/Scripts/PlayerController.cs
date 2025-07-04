using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 2.5f; // 跳跃高度
    [SerializeField] private float fallMultiplier = 1.5f; // 下落时的重力倍增系数
    [SerializeField] private float maxFallSpeed = 15f; // 最大下落速度
    [SerializeField] private float jumpBufferTime = 0.2f; // 跳跃缓冲时间，玩家可以提前0.2秒按下跳跃键
    private float jumpBufferCounter = 0f; // 跳跃缓冲计时器
    [Header("兔子跳设置")]
    [SerializeField] private float bunnyHopMomentumRetention = 0.95f; // 兔子跳时保留的动量百分比
    [SerializeField] private float bunnyHopWindow = 0.2f; // 落地后能执行兔子跳的时间窗口(秒)
    private float lastLandingTime = -10f; // 上次落地时间
    private float lastLandingVelocityX = 0f; // 上次落地时的水平速度
    [SerializeField] private float landingFrictionMultiplier = 2.5f;
    private bool justReleasedFromRope = false; // 是否刚从绳索脱离
    [Header("空中控制设置")]
    [SerializeField] private float airControlDuration = 0.5f; // 脱离绳索后可控制的时间（秒）
    [SerializeField] private float airControlForce = 5f; // 空中控制力度
    [SerializeField] private float maxAirSpeed = 50f;//空中最大速度

    private float airControlTimeRemaining = 0f; // 剩余的空中控制时间
    private float initialJumpVelocity; // 计算得出的初始跳跃速度
    private bool isJumping = false; // 是否正在跳跃
    private bool isAiming = false; // 是否正在瞄准
    private bool CanInput = true;
    private float originalgravityscale;

    [Header("钩索设置")]
    [SerializeField] private Transform aimIndicator;
    [SerializeField] public GameObject Gun; // 添加对箭头的引用
    [SerializeField] private float aimRotationSpeed = 120f;

    [Header("引用")]
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private StatusManager statusManager; // 添加对StatusManager的引用
    [SerializeField] private PlayerHealthManager healthManager;
    [SerializeField] private float swingForce = 5f;
    [SerializeField] private float maxSwingSpeed = 80f; // 调整这个值来设置最大速度

    [Header("物理材质设置")]
    [SerializeField] private PhysicsMaterial2D bouncyBallMaterial; // BouncyBall物理材质
    private PhysicsMaterial2D originalMaterial; // 存储原始物理材质

    [Header("跳跃动画设置")]
    private bool isPlayingJumpAnimation = false; // 是否正在播放跳跃动画
    // private bool isPlayingLandAnimation = false; // 是否正在播放着陆动画
    private bool pendingJump = false; // 是否有待执行的跳跃
    private bool isPendingBunnyHop = false;

    [Header("边缘辅助设置")]
    [SerializeField] private bool enableEdgeAssist = true; // 是否启用边缘辅助
    [SerializeField] private float edgeAssistCheckDistance = 0.8f; // 边缘检测距离
    [SerializeField] private float edgeAssistHeight = 0.5f; // 角色高度的一半，用于判断是否超过平台
    [SerializeField] private float edgeAssistForce = 5f; // 边缘辅助力度
    [SerializeField] private float edgeAssistDuration = 0.2f; // 边缘辅助持续时间
    [SerializeField] private LayerMask platformLayers => groundLayers; // 平台层级

    // 边缘辅助状态变量
    private bool isPerformingEdgeAssist = false;
    private float edgeAssistTimer = 0f;
    private Vector2 edgeAssistTargetPosition;

    // 落地挤压效果参数
    [Header("落地挤压效果")]
    [SerializeField] private bool enableLandingSquash = true;
    [SerializeField] private float squashIntensity = 0.3f; // 挤压强度
    [SerializeField] private float squashDuration = 0.2f; // 挤压持续时间
    [SerializeField] private float stretchDuration = 0.15f; // 拉伸恢复时间
    [SerializeField] private AnimationCurve squashCurve; // 挤压动画曲线
    private Vector3 originalScale; // 原始缩放
    private Coroutine squashCoroutine; // 当前运行的挤压协程
    private bool isSquashing = false; // 是否正在执行挤压效果




    [Header("地面检测设置")]
    [SerializeField] private LayerMask groundLayers; // 地面层
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f); // 地面检测区域的大小
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.05f); // 地面检测区域的偏移量
    [SerializeField] private bool showGroundCheck = true; // 是否在编辑器中显示地面检测区域

    [Header("斜坡设置")]

    [SerializeField] private float steepSlopeAngle = 45f; // 陡峭斜坡的角度阈值
    [SerializeField] private float slideSpeed = 5f; // 在陡峭斜坡上滑动的速度
    [SerializeField] private float slopeCheckDistance = 2.5f; // 斜坡检测距离
    [SerializeField] private float steepSlopeGracePeriod = 0.15f; // 陡峭斜坡宽限期，可在Inspector中调整
    private float steepSlopeTimer = 0f; // 在陡峭斜坡上的计时器
    private bool wasOnSteepSlope = false; // 上一帧是否在陡峭斜坡上

    private bool isOnSteepSlope = false; // 是否在陡峭斜坡上
    private Vector2 slopeNormalPerp; // 斜坡方向（垂直于法线）
    private float currentSlopeAngle = 0f; // 当前斜坡角度
    private float lastJumpTime = 0f; // 上次跳跃的时间

    [Header("头部检测设置")]
    [SerializeField] private Vector2 headCheckSize = new Vector2(0.9f, 0.1f); // 头部检测区域的大小
    [SerializeField] private Vector2 headCheckOffset = new Vector2(0f, 0.5f); // 头部检测区域的偏移量
    [SerializeField] private bool showHeadCheck = true; // 是否在编辑器中显示头部检测区域

    [Header("免疫设置")]
    [SerializeField] private bool isInvincible = false;    // 无敌状态
    [SerializeField] private bool isIceImmune = false;     // 冰免疫
    [SerializeField] private bool isFireImmune = false;    // 火免疫
    [SerializeField] private bool isElectricImmune = false; // 电免疫

    [Header("免疫视觉效果")]
    [SerializeField] private GameObject invincibleEffect;  // 无敌特效
    [SerializeField] private Color invincibleTint = new Color(1f, 1f, 1f, 1f); // 无敌时的颜色
    private SpriteRenderer playerRenderer;
    private Color originalColor;

    // 内部变量
    private Rigidbody2D rb;
    [SerializeField] private PlayerAnimationController playerAnimationController;
    private float aimAngle = 0f;
    private bool isGrounded = false;
    private bool isRopeMode = false;
    private bool CanShootRope = true;
    public bool debugmode = false;
    private DistanceJoint2D distanceJoint;

    // 缓存变量，用于减少null检查
    private bool rbInitialized = false;
    private bool distanceJointInitialized = false;
    private bool gunInitialized = false;
    private bool playerRendererInitialized = false;
    private bool ropeSystemInitialized = false;
    private bool statusManagerInitialized = false;
    private bool healthManagerInitialized = false;
    private bool invincibleEffectInitialized = false;

    // 性能优化：缓存检测结果和时间
    private float lastGroundCheckTime = 0f;
    private float groundCheckInterval = 0.05f; // 每0.05秒检测一次地面
    private bool cachedGroundedState = false;
    private float lastHeadCheckTime = 0f;
    private float headCheckInterval = 0.1f; // 每0.1秒检测一次头部
    private bool cachedHeadCollisionState = false;
    private Vector2 cachedPosition;
    private Coroutine[] immunityCoroutines = new Coroutine[4]; // 存储免疫协程的引用

    #region Unity methods
    private void Awake()
    {
        isRopeMode = false;
        rb = GetComponent<Rigidbody2D>();
        rbInitialized = rb != null;

        if (rbInitialized)
        {
            originalgravityscale = rb.gravityScale;
            cachedPosition = rb.position;
        }
        // 计算跳跃所需的速度和重力
        CalculateJumpParameters();

        if (ropeSystem == null)
            ropeSystem = GetComponentInChildren<RopeSystem>();
        ropeSystemInitialized = ropeSystem != null;

        if (statusManager == null)
            statusManager = GetComponentInChildren<StatusManager>();
        statusManagerInitialized = statusManager != null;

        if (healthManager == null)
            healthManager = GetComponentInChildren<PlayerHealthManager>();
        healthManagerInitialized = healthManager != null;

        // 获取SpriteRenderer组件
        playerRenderer = GetComponent<SpriteRenderer>();
        playerRendererInitialized = playerRenderer != null;
        if (playerRendererInitialized)
        {
            originalColor = playerRenderer.color;
        }

        InitializeBoundaries();

        // 初始化Gun
        gunInitialized = Gun != null;
        if (!gunInitialized)
        {
            Debug.LogError("Can't find player's Gun");
        }
        else
        {
            Gun.SetActive(CanShootRope);
        }

        // 确保无敌特效初始状态为关闭
        invincibleEffectInitialized = invincibleEffect != null;
        if (invincibleEffectInitialized)
        {
            invincibleEffect.SetActive(false);
        }

        // 保存原始物理材质
        if (rbInitialized)
        {
            originalMaterial = rb.sharedMaterial;
        }

        // 获取或添加DistanceJoint2D
        distanceJoint = GetComponent<DistanceJoint2D>();
        if (distanceJoint == null)
        {
            distanceJoint = gameObject.AddComponent<DistanceJoint2D>();
        }
        distanceJointInitialized = distanceJoint != null;

        // 确保初始时关节是禁用的
        if (distanceJointInitialized)
        {
            distanceJoint.enabled = false;
            distanceJoint.autoConfigureDistance = false;
            distanceJoint.enableCollision = true;
        }
    }

    private void Start()
    {
        originalScale = transform.localScale;
        // 如果需要，初始化挤压曲线
        if (squashCurve == null || squashCurve.keys.Length == 0)
        {
            squashCurve = new AnimationCurve();
            squashCurve.AddKey(0f, 0f);
            squashCurve.AddKey(0.1f, 1f);
            squashCurve.AddKey(0.6f, 0.8f);
            squashCurve.AddKey(1f, 0f);
        }
    }
    private Tilemap sourceTileMap;   
    private float minX, maxX, minY, maxY;
    private bool boundaryInitialized = false;

    private void InitializeBoundaries()
    {
        if (sourceTileMap == null)
        {
            Tilemap[] allTilemaps = FindObjectsOfType<Tilemap>();
            foreach (Tilemap tilemap in allTilemaps)
            {
                if (tilemap.name.Contains("SourceTileMap"))
                {
                    sourceTileMap = tilemap;
                    break;
                }
            }
        }

        // 如果还是找不到，记录警告并返回
        if (sourceTileMap == null)
        {
            Debug.LogWarning("未找到sourceTileMap，无法初始化地图边界！");
            return;
        }
        

        // 获取Tilemap的边界
        sourceTileMap.CompressBounds();
        BoundsInt bounds = sourceTileMap.cellBounds;

        // 转换为世界坐标
        Vector3 minWorld = sourceTileMap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
        Vector3 maxWorld = sourceTileMap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0));

        // 考虑Tilemap单元格大小
        Vector3 cellSize = sourceTileMap.cellSize;
        maxWorld += new Vector3(cellSize.x, cellSize.y, 0);

        // 设置边界值
        minX = minWorld.x;
        maxX = maxWorld.x;
        minY = minWorld.y;
        maxY = maxWorld.y;

        boundaryInitialized = true;

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("地图边界已初始化: X({0}, {1}), Y({2}, {3})", minX, maxX, minY, maxY);
#endif
    }

    private void CheckBoundaries()
    {
        // 如果边界检测被禁用、玩家已经无敌或边界未初始化，直接返回
        if (!boundaryInitialized)
            return;

        // 获取当前玩家位置
        Vector2 playerPosition = transform.position;

        // 检查玩家是否超出边界
        bool outOfBounds;


        // 检查所有边界
        outOfBounds = playerPosition.x < minX ||
                        playerPosition.x > maxX ||
                        playerPosition.y < minY ||
                        playerPosition.y > maxY;


        // 如果超出边界，触发玩家死亡
        if (outOfBounds)
        {
            // 确定死亡原因（用于日志或特效）
            string deathReason = "未知";
            if (playerPosition.y < minY)
                deathReason = "掉出底部边界";
            else if (playerPosition.y > maxY)
                deathReason = "超出顶部边界";
            else if (playerPosition.x < minX)
                deathReason = "超出左侧边界";
            else if (playerPosition.x > maxX)
                deathReason = "超出右侧边界";

            // 记录日志
#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("玩家超出地图边界，原因: {0}, 位置: {1}", deathReason, playerPosition);
#endif

            // 触发玩家死亡事件
            healthManager.KillPlayer();
        }
    }

    private float horizontalInput;

    private void Update()
    {
        // 性能优化：只在位置变化时更新缓存的位置
        if (rbInitialized && rb.position != cachedPosition)
        {
            cachedPosition = rb.position;
        }

        // 性能优化：减少Gun状态检查频率
        if (gunInitialized)
        {
            bool shouldShowGun = CanShootRope && !isRopeMode && ropeSystemInitialized && !ropeSystem.IsShooting() && !ropeSystem.IsRopeShootingOrHooked();
            if (Gun.activeSelf != shouldShowGun)
            {
                Gun.SetActive(shouldShowGun);
            }
        }

        horizontalInput = Input.GetAxis("Horizontal");

        if (isRopeMode)
        {
            HandleRopeMode();
        }
        else
        {
            HandleNormalMode();

            // 性能优化：减少地面检测频率
            if (Time.time >= lastGroundCheckTime + groundCheckInterval)
            {
                CheckGrounded();
                lastGroundCheckTime = Time.time;
            }

            // 处理下落加速
            HandleFallingGravity();
        }
        CheckLandSpeed();
    }

    private void CheckLandSpeed()
    {
        if (enableLandingSquash && rbInitialized && !ropeSystem.IsRopeShootingOrHooked())
        {
            // 检测从空中落到地面的状态变化
            if (isGrounded && rb.velocity.y < -5f) // 只有当下落速度足够大时才触发
            {
                // 记录落地速度，用于调整挤压强度
                float landingVelocity = Mathf.Abs(rb.velocity.y);

                // 根据落地速度计算挤压强度，限制在合理范围内
                float actualSquashIntensity = Mathf.Clamp(landingVelocity / 20f * squashIntensity, 0.1f, 0.5f);

                // 执行落地挤压效果
                PlayLandingSquashEffect(actualSquashIntensity);

#if UNITY_EDITOR
                if (debugmode)
                    Debug.LogFormat("检测到落地，速度: {0}, 挤压强度: {1}", landingVelocity, actualSquashIntensity);
#endif
            }
        }
    }

    // 播放落地挤压效果
    private void PlayLandingSquashEffect(float intensity)
    {
        // 如果已经在执行挤压效果，先停止当前协程
        if (isSquashing && squashCoroutine != null)
        {
            StopCoroutine(squashCoroutine);
        }

        // 启动新的挤压协程
        squashCoroutine = StartCoroutine(SquashAndStretchCoroutine(intensity));
#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("开始落地挤压效果，强度: {0}", intensity);
#endif
    }

    // 挤压和拉伸协程
    private IEnumerator SquashAndStretchCoroutine(float intensity)
    {
        isSquashing = true;

        // 记录开始时间
        float startTime = Time.time;
        float elapsedTime = 0f;

        // 第一阶段：挤压效果
        while (elapsedTime < squashDuration)
        {
            // 计算当前时间点的插值比例
            float t = elapsedTime / squashDuration;

            // 使用动画曲线获取当前时间点的挤压值
            float curveValue = squashCurve.Evaluate(t);

            // 计算当前应用的挤压和拉伸值
            float xScale = originalScale.x * (1f + intensity * curveValue);
            float yScale = originalScale.y * (1f - intensity * curveValue);

            // 应用缩放
            transform.localScale = new Vector3(xScale, yScale, originalScale.z);

            // 更新经过的时间
            elapsedTime = Time.time - startTime;

            yield return null;
        }

        // 第二阶段：恢复原状（拉伸效果）
        startTime = Time.time;
        elapsedTime = 0f;

        // 记录挤压阶段结束时的缩放值
        Vector3 squashedScale = transform.localScale;

        while (elapsedTime < stretchDuration)
        {
            // 计算当前时间点的插值比例
            float t = elapsedTime / stretchDuration;

            // 使用平滑步进函数进行插值，实现弹性恢复效果
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // 从挤压状态平滑过渡到原始缩放
            transform.localScale = Vector3.Lerp(squashedScale, originalScale, smoothT);

            // 更新经过的时间
            elapsedTime = Time.time - startTime;

            yield return null;
        }

        // 确保最终恢复到原始缩放
        transform.localScale = originalScale;

        isSquashing = false;
    }

    private void CalculateJumpParameters()
    {
        // 使用物理公式计算跳跃初始速度
        // 公式: v = sqrt(2 * h * g)
        // 其中 h 是跳跃高度，g 是重力加速度
        if (rbInitialized)
        {
            float gravity = Physics2D.gravity.magnitude * rb.gravityScale;
            initialJumpVelocity = Mathf.Sqrt(2 * jumpHeight * gravity);
        }
    }

    #endregion
    #region check head&ground collision
    public bool wasGrounded;

    // 性能优化：减少地面检测频率
    private void CheckGrounded()
    {
        // 保存上一帧的着地状态和斜坡状态
        wasGrounded = isGrounded;
        wasOnSteepSlope = isOnSteepSlope;

        // 先重置陡峭斜坡状态，让后续检测重新判断
        isOnSteepSlope = false;

        // 进行地面检测，包括斜坡检测
        cachedGroundedState = CheckNormalModeGroundCollision();

        // 处理陡峭斜坡计时器
        HandleSteepSlopeTimer();

        // 根据计时器决定是否应用斜坡效果
        bool shouldApplySlopeEffect = isOnSteepSlope && steepSlopeTimer >= steepSlopeGracePeriod;

        // 设置最终的接地状态
        isGrounded = cachedGroundedState && !shouldApplySlopeEffect;

        // 如果着地状态发生变化，触发事件
        if (wasGrounded != isGrounded)
        {
            GameEvents.TriggerPlayerGroundedStateChanged(isGrounded);

            // 如果刚刚落地，记录当前时间和水平速度
            if (isGrounded && rbInitialized)
            {
                // 记录落地时间和速度，用于兔子跳判断
                lastLandingTime = Time.time;
                lastLandingVelocityX = rb.velocity.x;
#if UNITY_EDITOR
                if (debugmode && Mathf.Abs(lastLandingVelocityX) > 3f)
                    Debug.LogFormat("落地时水平速度: {0}", lastLandingVelocityX);
#endif
            }
        }

        // 玩家在陡峭斜坡上且超过宽限期，应用滑动效果
        if (shouldApplySlopeEffect && rbInitialized)
        {
            // 这样可以保持滑动的流畅性，同时不影响普通地面移动
            rb.velocity = slopeNormalPerp * slideSpeed;
            // 增加额外的向下力，使滑动更流畅
            rb.AddForce(Vector2.down * 2f, ForceMode2D.Force);
        }
    }

    private void HandleSteepSlopeTimer()
    {
        // 如果检测到陡峭斜坡
        if (isOnSteepSlope)
        {
            // 如果是首次进入斜坡，或者从跳跃状态落到斜坡上
            if (!wasOnSteepSlope || isJumping)
            {
                // 重置计时器
                steepSlopeTimer = 0f;
            }
            else
            {
                // 增加计时器
                steepSlopeTimer += Time.deltaTime;
            }
        }
        else
        {
            // 不在陡峭斜坡上时重置计时器
            steepSlopeTimer = 0f;
        }

#if UNITY_EDITOR
        if (showGroundCheck && isOnSteepSlope)
        {
            // 可视化计时器进度
            float progress = Mathf.Clamp01(steepSlopeTimer / steepSlopeGracePeriod);
            Color timerColor = Color.Lerp(Color.green, Color.red, progress);
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 0.5f, timerColor);
        }
#endif
    }

    // 添加辅助方法用于旋转点
    private Vector2 RotatePoint(Vector2 point, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            point.x * cos - point.y * sin,
            point.x * sin + point.y * cos
        );
    }

    // 性能优化：减少头部检测频率
    private bool CheckHeadCollision()
    {
        // 使用缓存的结果，如果时间间隔不够
        if (Time.time < lastHeadCheckTime + headCheckInterval)
        {
            return cachedHeadCollisionState;
        }

        lastHeadCheckTime = Time.time;

        // 如果在绳索模式下，使用不同的检测逻辑
        if (isRopeMode && ropeSystemInitialized && ropeSystem.HasAnchors())
        {
            // 获取绳索方向
            Vector2 ropeDirection = (ropeSystem.GetCurrentAnchorPosition() - (Vector2)transform.position).normalized;

            // 计算检测区域的中心点 - 沿绳索方向偏移
            Vector2 position = transform.position;
            Vector2 center = position + ropeDirection * 0.5f;

            // 计算检测区域的旋转角度
            float angle = Mathf.Atan2(ropeDirection.y, ropeDirection.x) * Mathf.Rad2Deg;

            // 使用OverlapBox检测指定区域内的碰撞体，考虑旋转
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, headCheckSize, angle, groundLayers);

            // 在编辑器中可视化检测区域
#if UNITY_EDITOR
            if (showHeadCheck)
            {
                // 计算旋转后的四个角点
                Vector2 halfSize = headCheckSize * 0.5f;
                Vector2 bottomLeft = RotatePoint(new Vector2(-halfSize.x, -halfSize.y), angle);
                Vector2 bottomRight = RotatePoint(new Vector2(halfSize.x, -halfSize.y), angle);
                Vector2 topRight = RotatePoint(new Vector2(halfSize.x, halfSize.y), angle);
                Vector2 topLeft = RotatePoint(new Vector2(-halfSize.x, halfSize.y), angle);

                Color debugColor = colliders.Length > 0 ? Color.green : Color.red;
                Debug.DrawLine(center + bottomLeft, center + bottomRight, debugColor);
                Debug.DrawLine(center + bottomRight, center + topRight, debugColor);
                Debug.DrawLine(center + topRight, center + topLeft, debugColor);
                Debug.DrawLine(center + topLeft, center + bottomLeft, debugColor);
            }
#endif

            // 缓存并返回结果
            cachedHeadCollisionState = colliders.Length > 0;
            return cachedHeadCollisionState;
        }
        else
        {
            // 原有的头部检测逻辑
            Vector2 position = transform.position;
            Vector2 center = position + headCheckOffset;
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, headCheckSize, 0f, groundLayers);

#if UNITY_EDITOR
            if (showHeadCheck)
            {
                Color debugColor = colliders.Length > 0 ? Color.green : Color.red;
                Vector3 bottomLeft = new Vector3(center.x - headCheckSize.x / 2, center.y - headCheckSize.y / 2);
                Vector3 bottomRight = new Vector3(center.x + headCheckSize.x / 2, center.y - headCheckSize.y / 2);
                Vector3 topRight = new Vector3(center.x + headCheckSize.x / 2, center.y + headCheckSize.y / 2);
                Vector3 topLeft = new Vector3(center.x - headCheckSize.x / 2, center.y + headCheckSize.y / 2);

                Debug.DrawLine(bottomLeft, bottomRight, debugColor);
                Debug.DrawLine(bottomRight, topRight, debugColor);
                Debug.DrawLine(topRight, topLeft, debugColor);
                Debug.DrawLine(topLeft, bottomLeft, debugColor);
            }
#endif

            // 缓存并返回结果
            cachedHeadCollisionState = colliders.Length > 0;
            return cachedHeadCollisionState;
        }
    }

    // 绳索模式下的地面检测
    private bool CheckRopeModeGroundCollision()
    {
        // 获取绳索方向
        Vector2 ropeDirection = (ropeSystem.GetCurrentAnchorPosition() - (Vector2)transform.position).normalized;

        // 计算检测区域的中心点 - 沿绳索反方向偏移
        Vector2 position = transform.position;
        Vector2 center = position - ropeDirection * 0.25f;

        // 计算检测区域的旋转角度
        float angle = Mathf.Atan2(ropeDirection.y, ropeDirection.x) * Mathf.Rad2Deg;

        // 使用OverlapBox检测指定区域内的碰撞体，考虑旋转
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, groundCheckSize, angle, groundLayers);

        // 在编辑器中可视化检测区域
#if UNITY_EDITOR
        if (showGroundCheck)
        {
            // 计算旋转后的四个角点
            Vector2 halfSize = groundCheckSize * 0.5f;
            Vector2 bottomLeft = RotatePoint(new Vector2(-halfSize.x, -halfSize.y), angle);
            Vector2 bottomRight = RotatePoint(new Vector2(halfSize.x, -halfSize.y), angle);
            Vector2 topRight = RotatePoint(new Vector2(halfSize.x, halfSize.y), angle);
            Vector2 topLeft = RotatePoint(new Vector2(-halfSize.x, halfSize.y), angle);

            Color debugColor = colliders.Length > 0 ? Color.green : Color.red;
            Debug.DrawLine(center + bottomLeft, center + bottomRight, debugColor);
            Debug.DrawLine(center + bottomRight, center + topRight, debugColor);
            Debug.DrawLine(center + topRight, center + topLeft, debugColor);
            Debug.DrawLine(center + topLeft, center + bottomLeft, debugColor);
        }
#endif

        // 如果检测到任何碰撞体，则认为有地面支撑
        return colliders.Length > 0;
    }

    // 普通模式下的地面检测
    private bool CheckNormalModeGroundCollision()
{
    Vector2 position = transform.position;
    // 增加检测范围，使用更宽的检测框
    Vector2 center = position + groundCheckOffset;
    Vector2 enlargedSize = new Vector2(groundCheckSize.x, groundCheckSize.y * 1.2f); // 稍微增加高度以适应斜坡
    
    // 使用统一的射线检测来处理地面、斜坡和悬崖边缘
    bool isGroundDetected = CheckGroundAndSlopeWithRaycasts(position);
    
    // 如果在陡峭斜坡上，但不是卡住的情况，则不算作接地
    if (isOnSteepSlope)
    {
        VisualizeGroundCheckBox(center, enlargedSize, false, Color.yellow); // 使用黄色表示陡峭斜坡
        return false; // 在陡峭斜坡上不算作接地
    }
    
    // 只有当射线检测通过时才进行盒检测
    if (isGroundDetected)
    {
        // 常规的盒检测作为额外检查
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, enlargedSize, 0f, groundLayers);
        bool boxDetected = colliders.Length > 0;
        
        // 可视化地面检测框
        VisualizeGroundCheckBox(center, enlargedSize, boxDetected);
        
        return boxDetected;
    }
    else
    {
        // 如果射线检测失败（可能是在悬崖边缘），显示红色检测框
        VisualizeGroundCheckBox(center, enlargedSize, false, Color.red);
        return false;
    }
}

// 只使用射线检测斜坡属性，不返回地面检测结果
private bool CheckGroundAndSlopeWithRaycasts(Vector2 position)
{
    // 重置斜坡状态
    isOnSteepSlope = false;
    currentSlopeAngle = 0f;
    bool isOnCliffEdge = false;  // 新增：是否在悬崖边缘
    
    bool raycastHit = false;
    float rayDistance = slopeCheckDistance;
    
    // 使用3条射线进行检测
    int rayCount = 3;
    RaycastHit2D[] hits = new RaycastHit2D[rayCount];
    bool[] hitResults = new bool[rayCount];
    
    // 发射射线并收集结果
    for (int i = 0; i < rayCount; i++)
    {
        float xOffset = ((float)i / (rayCount - 1) - 0.5f) * groundCheckSize.x;
        Vector2 rayStart = new Vector2(position.x + xOffset, position.y);
        hits[i] = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, groundLayers);
        hitResults[i] = hits[i].collider != null;
        
        if (hitResults[i])
        {
            raycastHit = true;
        }
        
        #if UNITY_EDITOR
        if (showGroundCheck)
        {
            // 使用紫色表示这些是斜坡检测射线
            Color rayColor = hitResults[i] ? Color.magenta : Color.red;
            Debug.DrawRay(rayStart, Vector2.down * rayDistance, rayColor);
            
            if (hitResults[i])
            {
                // 显示法线方向
                Debug.DrawRay(hits[i].point, hits[i].normal * 0.3f, Color.white);
            }
        }
        #endif
    }
    
    // 检测悬崖边缘：左右两侧射线命中，但中间射线未命中
    int centerIndex = rayCount / 2;
    if (hitResults[0] && hitResults[rayCount - 1] && !hitResults[centerIndex])
    {
        isOnCliffEdge = true;
        
        #if UNITY_EDITOR
        if (showGroundCheck && debugmode)
        {
            Debug.LogFormat("检测到悬崖边缘！");
            // 在角色位置上方显示警告标记
            Debug.DrawLine(position, position + Vector2.up * 0.5f, Color.red);
            Debug.DrawLine(position + Vector2.up * 0.5f - Vector2.right * 0.25f, 
                          position + Vector2.up * 0.5f + Vector2.right * 0.25f, Color.red);
        }
        #endif
        
        // 如果在悬崖边缘，应用向下的力
        if (rbInitialized)
        {
            // 给一个较小的向下速度，让玩家开始滑落
            float cliffSlideSpeed = 2.0f;  // 可以调整这个值
            rb.velocity = new Vector2(rb.velocity.x, -cliffSlideSpeed);
            
            #if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("在悬崖边缘滑落，应用向下速度: {0}", -cliffSlideSpeed);
            #endif
        }
        
        // 返回false表示不在地面上
        return false;
    }
    
    // 处理斜坡检测 - 至少需要中间射线和一侧射线都命中
    if (hitResults[centerIndex] && (hitResults[0] || hitResults[rayCount - 1]))
    {
        // 计算中间射线的斜坡角度
        float centerSlopeAngle = Vector2.Angle(hits[centerIndex].normal, Vector2.up);
        
        // 计算另一侧射线的斜坡角度
        float sideSlopeAngle = 0f;
        if (hitResults[0])
        {
            sideSlopeAngle = Vector2.Angle(hits[0].normal, Vector2.up);
        }
        else if (hitResults[rayCount - 1])
        {
            sideSlopeAngle = Vector2.Angle(hits[rayCount - 1].normal, Vector2.up);
        }
        
        // 取平均斜坡角度
        float averageSlopeAngle = (centerSlopeAngle + sideSlopeAngle) / 2f;
        
        // 存储当前斜坡角度供其他方法使用
        currentSlopeAngle = averageSlopeAngle;
        
        // 计算斜坡方向（垂直于法线）- 使用中心射线的法线
        slopeNormalPerp = Vector2.Perpendicular(hits[centerIndex].normal).normalized;
        
        // 确保斜坡方向朝下
        if (slopeNormalPerp.y > 0)
            slopeNormalPerp = -slopeNormalPerp;
        
        // 如果斜坡角度大于等于陡峭斜坡角度，则检查是否卡在斜坡上
        if (averageSlopeAngle >= steepSlopeAngle)
        {

            isOnSteepSlope = true;
            
            #if UNITY_EDITOR
            if (showGroundCheck)
            {
                Debug.DrawRay(position, Vector2.down * rayDistance * 0.5f, Color.blue);
                
                if (debugmode && Time.frameCount % 30 == 0)
                {
                    Debug.LogFormat("在陡峭斜坡上，角度: {0}", averageSlopeAngle);
                }
            }
            #endif
        }
    }
    
    return raycastHit && !isOnCliffEdge;
}

// 可视化地面检测框
private void VisualizeGroundCheckBox(Vector2 center, Vector2 size, bool hasCollision, Color? overrideColor = null)
{
    #if UNITY_EDITOR
    if (showGroundCheck)
    {
        Color debugColor = overrideColor ?? (hasCollision ? Color.green : Color.red);
        // 绘制检测框
        Vector2 bottomLeft = center + new Vector2(-size.x / 2, -size.y / 2);
        Vector2 bottomRight = center + new Vector2(size.x / 2, -size.y / 2);
        Vector2 topRight = center + new Vector2(size.x / 2, size.y / 2);
        Vector2 topLeft = center + new Vector2(-size.x / 2, size.y / 2);
        Debug.DrawLine(bottomLeft, bottomRight, debugColor);
        Debug.DrawLine(bottomRight, topRight, debugColor);
        Debug.DrawLine(topRight, topLeft, debugColor);
        Debug.DrawLine(topLeft, bottomLeft, debugColor);
    }
    #endif
}

    #endregion



    #region handle Input
    private void HandleNormalMode()
    {
        if (!CanInput)
            return;

        bool isRopeBusy = ropeSystemInitialized && ropeSystem.IsRopeShootingOrHooked();


        // 处理玩家朝向
        HandleRunningFacing(horizontalInput);

        // 处理移动逻辑
        HandleMovement(horizontalInput, isRopeBusy);

        // 处理瞄准控制
        HandleAiming(isRopeBusy);

        // 处理跳跃输入和缓冲
        HandleJumpInput(isRopeBusy);

        // 处理道具使用
        HandleItemUse(isRopeBusy);

        // 处理绳索发射
        HandleRopeShooting(isRopeBusy);
    }

    // 处理地面和空中移动
    private void HandleMovement(float horizontalInput, bool isRopeBusy)
    {
        // 创建局部变量，可以在方法内修改
        float _cachedhorizontalInput = horizontalInput;

        if (isGrounded && rbInitialized)
        {
            HandleGroundMovement(_cachedhorizontalInput);
        }
        else if (rbInitialized)
        {
            HandleAirMovement(_cachedhorizontalInput);
        }

        // 根据移动方向更新玩家朝向 - 只在绳索未发射时
        if (_cachedhorizontalInput != 0 && !isRopeBusy)
        {
            // 如果向右移动，朝向右边
            if (_cachedhorizontalInput > 0)
            {
                aimIndicator.transform.localScale = new Vector3(1, 1, 1); // 正常比例，朝右
            }
            // 如果向左移动，朝向左边
            else if (_cachedhorizontalInput < 0)
            {
                aimIndicator.transform.localScale = new Vector3(-1, 1, 1); // X轴反转，朝左
            }
        }
    }

    // 处理地面移动
    private float HandleGroundMovement(float _cachedhorizontalInput)
    {
        // 在地面上时完全控制移动
        airControlTimeRemaining = airControlDuration;

        // 检查是否在陡峭斜坡上
        if (isOnSteepSlope)
        {
            // 正常斜坡滑动
            rb.velocity = slopeNormalPerp * slideSpeed;
            // 增加额外的向下力，使滑动更流畅
            rb.AddForce(Vector2.down * 2f, ForceMode2D.Force);
            _cachedhorizontalInput = 0f;

#if UNITY_EDITOR
            if (debugmode && Time.frameCount % 30 == 0)
                Debug.LogFormat("在陡峭斜坡上滑动，速度: {0}", rb.velocity);
#endif

            // 如果刚刚落地，重置跳跃状态
            if (isJumping)
            {
                isJumping = false;
            }

            return _cachedhorizontalInput; // 提前返回，不执行下面的普通地面移动逻辑
        }

        // 检查是否在斜坡上，以及移动方向是否是上坡
        bool isMovingUpSlope = CheckIfMovingUpSlope(_cachedhorizontalInput);

        // 检查是否在兔子跳时间窗口内
        bool inBunnyHopWindow = (Time.time - lastLandingTime) <= bunnyHopWindow;
        bool hasSignificantLandingSpeed = Mathf.Abs(lastLandingVelocityX) > 3f;

        // 根据不同情况应用不同的移动逻辑
        if (inBunnyHopWindow && hasSignificantLandingSpeed)
        {
            ApplyBunnyHopMovement(_cachedhorizontalInput);
        }
        else
        {
            ApplyNormalGroundMovement(_cachedhorizontalInput, isMovingUpSlope);
        }

        // 如果刚刚落地，重置跳跃状态
        if (isJumping)
        {
            isJumping = false;
            // 可以在这里添加落地音效或动画触发
        }
    return _cachedhorizontalInput;
}
    // 检查是否在上坡移动
    private bool CheckIfMovingUpSlope(float horizontalInput)
    {
        if (currentSlopeAngle <= 0f || Mathf.Abs(horizontalInput) < 0.1f)
            return false;

        // 获取当前站立的斜坡法线
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, slopeCheckDistance, groundLayers);
        if (hit.collider != null)
        {
            // 计算斜坡方向（垂直于法线）
            Vector2 slopeDirection = new Vector2(hit.normal.y, -hit.normal.x);

            // 判断玩家移动方向是否与斜坡上坡方向一致
            float dotProduct = Vector2.Dot(new Vector2(horizontalInput, 0), slopeDirection);
            return dotProduct > 0;
        }
        return false;
    }

    // 应用兔子跳移动逻辑
    private void ApplyBunnyHopMovement(float horizontalInput)
    {
        // 如果没有输入，或输入方向与落地速度方向相反，应用摩擦力
        if (Mathf.Abs(horizontalInput) < 0.1f || Mathf.Sign(horizontalInput) != Mathf.Sign(lastLandingVelocityX))
        {
            // 应用摩擦力，减缓水平速度
            float frictionForce = landingFrictionMultiplier * Time.deltaTime;
            float newVelocityX = Mathf.MoveTowards(rb.velocity.x, 0, frictionForce);
            rb.velocity = new Vector2(newVelocityX, rb.velocity.y);

#if UNITY_EDITOR
            if (debugmode && Time.frameCount % 10 == 0)
                Debug.LogFormat("应用落地摩擦力: {0} -> {1}", rb.velocity.x, newVelocityX);
#endif
        }
        // 如果有输入且方向与落地速度相同，保持部分动量
        else if (Mathf.Sign(horizontalInput) == Mathf.Sign(lastLandingVelocityX))
        {
            // 计算目标速度 (结合输入和原有动量)
            float targetSpeed = horizontalInput * moveSpeed;
            // 如果落地速度更大，使用落地速度的一部分
            if (Mathf.Abs(lastLandingVelocityX) > Mathf.Abs(targetSpeed))
            {
                targetSpeed = lastLandingVelocityX * 0.9f; // 保留90%的落地速度
            }
            rb.velocity = new Vector2(targetSpeed, rb.velocity.y);
        }
    }

    // 应用普通地面移动逻辑,处理陡峭斜坡上的滑动
    private void ApplyNormalGroundMovement(float horizontalInput, bool isMovingUpSlope)
    {
        // 如果在普通斜坡上且尝试上坡
        if (currentSlopeAngle > 0 && isMovingUpSlope)
        {
            // 正常移动，玩家可以控制
            rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
        }
        // 普通地面移动
        else
        {
            rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
        }
    }

    // 处理空中移动
    private void HandleAirMovement(float horizontalInput)
    {
        // 原有的空中控制逻辑
        if (!isGrounded && airControlTimeRemaining > 0)
        {
            // 减少剩余控制时间
            airControlTimeRemaining -= Time.deltaTime;

            // 在有限时间内可以控制，但只有反方向的输入有效
            if (airControlTimeRemaining > 0)
            {
                ApplyAirControl(horizontalInput);
            }

            // 限制最大空中速度
            LimitAirSpeed();
        }

        // 添加边缘辅助检测
        if (enableEdgeAssist && !isGrounded && rb.velocity.y <= 0)
        {
            CheckForEdgeAssist(horizontalInput);
        }

        // 如果正在执行边缘辅助，应用辅助力
        if (isPerformingEdgeAssist)
        {
            ApplyEdgeAssist();
        }
    }

    // 检测是否需要边缘辅助
    private void CheckForEdgeAssist(float horizontalInput)
{
    // 如果已经在执行边缘辅助或没有水平输入，直接返回
    if (isPerformingEdgeAssist || Mathf.Abs(horizontalInput) < 0.1f)
        return;

    // 确定检测方向
    Vector2 direction = new Vector2(Mathf.Sign(horizontalInput), 0);
    
    // 从角色位置向移动方向发射射线，检测是否有墙壁
    RaycastHit2D hit = Physics2D.Raycast(
        transform.position,
        direction,
        edgeAssistCheckDistance,
        platformLayers
    );
    
    // 如果没有检测到墙壁，直接返回
    if (hit.collider == null)
        return;
        
    // 修改：从检测到的点向内部偏移一小段距离，然后向上发射射线
    Vector2 edgePosition = hit.point;
    // 向墙内部偏移一点距离
    Vector2 insidePosition = edgePosition + direction * 0.25f;
    
    // 从内部位置向上发射射线，检测平台顶部
    RaycastHit2D upHit = Physics2D.Raycast(
        insidePosition,
        Vector2.up,
        edgeAssistHeight * 2, // 增加检测距离以确保能找到顶部
        platformLayers
    );
    
    // 如果没有检测到平台顶部，直接返回
    if (upHit.collider == null)
        return;
        
    // 修改：从检测到的墙体顶部位置向下发射射线，确认平台顶部精确位置
    Vector2 abovePlatform = new Vector2(insidePosition.x, upHit.point.y + 0.1f);
    RaycastHit2D topHit = Physics2D.Raycast(
        abovePlatform,
        Vector2.down,
        0.2f,
        platformLayers
    );
    
    // 如果无法确定平台顶部，直接返回
    if (topHit.collider == null)
        return;
        
    // 计算角色底部与平台顶部的高度差
    float heightDifference = topHit.point.y - (transform.position.y - edgeAssistHeight);
    
    // 如果角色至少有一半高度超过平台，启动边缘辅助
    if (heightDifference > 0 && heightDifference < edgeAssistHeight * 2)
    {
        // 设置边缘辅助目标位置（平台顶部稍微向内一点的位置）
        edgeAssistTargetPosition = new Vector2(
            topHit.point.x + direction.x * 0.3f,
            topHit.point.y + edgeAssistHeight + 0.05f
        );
        
        // 启动边缘辅助
        isPerformingEdgeAssist = true;
        edgeAssistTimer = edgeAssistDuration;
        
        #if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("启动边缘辅助: 目标位置 = {0}", edgeAssistTargetPosition);
        #endif
    }
}

private void ApplyEdgeAssist()
{
    // 确保计时器正确更新
    edgeAssistTimer -= Time.deltaTime;
    
    #if UNITY_EDITOR
    if (debugmode)
    {
        Debug.LogFormat("边缘辅助状态: 计时器={0:F2}, 位置={1}, 目标={2}",
            edgeAssistTimer, transform.position, edgeAssistTargetPosition);
    }
    #endif
    
    // 强制检查：如果计时器小于等于0，一定要停止
    if (edgeAssistTimer <= 0)
    {
        CompleteEdgeAssist("时间结束");
        return;
    }
    
    // 计算到目标位置的距离
    float distanceToTarget = Vector2.Distance(transform.position, edgeAssistTargetPosition);
    
    // 如果已经非常接近目标，也完成辅助
    if (distanceToTarget < 0.1f)
    {
        CompleteEdgeAssist("到达目标位置");
        return;
    }
    
    // 计算方向并应用力
    Vector2 directionToTarget = (edgeAssistTargetPosition - (Vector2)transform.position);
    Vector2 normalizedDirection = directionToTarget.normalized;
    
    // 应用力以移动角色到目标位置
    rb.velocity = normalizedDirection * edgeAssistForce;
}

// 抽取完成边缘辅助的逻辑到单独的方法
private void CompleteEdgeAssist(string reason)
{
    // 直接设置位置，确保Y坐标正确，但保留当前的X坐标
    transform.position = new Vector2(transform.position.x, edgeAssistTargetPosition.y);
    
    // 重置速度，防止弹跳
    rb.velocity = Vector2.zero;
    
    // 确保标志被重置
    isPerformingEdgeAssist = false;
    
    // 重置计时器，确保不会再次触发
    edgeAssistTimer = 0;
    
    #if UNITY_EDITOR
    if (debugmode)
    {
        Debug.LogFormat("边缘辅助已完成，原因: {0}", reason);
    }
    #endif
}

    // 应用空中控制力
    private void ApplyAirControl(float horizontalInput)
    {
        // 获取当前水平速度方向
        float currentVelocityDirection = Mathf.Sign(rb.velocity.x);

        // 获取输入方向
        float inputDirection = Mathf.Sign(horizontalInput);

        // 只有当输入方向与当前速度方向相反时才施加力
        if (currentVelocityDirection != 0 && inputDirection != 0 && inputDirection != currentVelocityDirection)
        {
            // 施加力以控制方向，但不影响原有动量
            rb.AddForce(new Vector2(horizontalInput * airControlForce, 0), ForceMode2D.Force);
        }
        // 如果玩家速度接近0，允许任意方向输入以开始移动
        else if (Mathf.Abs(rb.velocity.x) < 0.5f)
        {
            rb.AddForce(new Vector2(horizontalInput * airControlForce, 0), ForceMode2D.Force);
        }
    }

    // 限制空中最大速度
    private void LimitAirSpeed()
    {
        if (Mathf.Abs(rb.velocity.x) > maxAirSpeed)
        {
            float clampedXVelocity = Mathf.Clamp(rb.velocity.x, -maxAirSpeed, maxAirSpeed);
            rb.velocity = new Vector2(clampedXVelocity, rb.velocity.y);
        }
    }

    // 处理瞄准控制
    private void HandleAiming(bool isRopeBusy)
    {
        if (isRopeBusy)
        {
            // 在绳索模式下，设置isAiming为false
            isAiming = false;
            return;
        }

        // 检测是否按下上箭头或下箭头键
        bool isPressingUpOrDown = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);

        // 如果按下了上箭头或下箭头键，设置isAiming为true
        if (isPressingUpOrDown)
        {
            isAiming = true;

            // 处理上箭头键
            if (Input.GetKey(KeyCode.UpArrow))
            {
                aimAngle += aimRotationSpeed * Time.deltaTime;
            }

            // 处理下箭头键
            if (Input.GetKey(KeyCode.DownArrow))
            {
                aimAngle -= aimRotationSpeed * Time.deltaTime;
            }
        }
        else
        {
            // 如果没有按下上箭头或下箭头键，设置isAiming为false
            isAiming = false;
        }

        // 限制瞄准角度
        aimAngle = Mathf.Clamp(aimAngle, -80f, 80f);

        // 根据 aimIndicator 的水平翻转调整旋转角度
        float flipMultiplier = aimIndicator.localScale.x > 0 ? 1 : -1; // 如果翻转，角度需要反向
        aimIndicator.rotation = UnityEngine.Quaternion.Euler(0, 0, aimAngle * flipMultiplier);
    }

    // 处理跳跃输入和缓冲
    private void HandleJumpInput(bool isRopeBusy)
    {
        // 如果已经在播放跳跃动画，直接返回
        if (isPlayingJumpAnimation || pendingJump)
            return;

        // 跳跃输入检测 - 使用缓冲跳跃机制
        if (Input.GetKeyDown(KeyCode.X) && !isRopeBusy)
        {
            // 设置跳跃缓冲计时器
            jumpBufferCounter = jumpBufferTime;

#if UNITY_EDITOR
            if (debugmode)
            {
                Debug.LogFormat("跳跃输入已缓冲: {0}秒", jumpBufferTime);
                Debug.LogFormat("跳跃条件状态: isGrounded={0}, isOnSteepSlope={1}, steepSlopeTimer={2:F2}, currentSlopeAngle={3:F1}",
                    isGrounded, isOnSteepSlope, steepSlopeTimer, currentSlopeAngle);
            }
#endif
        }

        // 更新跳跃缓冲计时器
        if (jumpBufferCounter > 0)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 如果没有跳跃缓冲，直接返回
        if (jumpBufferCounter <= 0 || isJumping || isRopeBusy || !rbInitialized)
            return;

        // 分别处理普通地面跳跃和斜坡跳跃
        if (isGrounded)
        {
            // 普通地面跳跃
            TriggerJump();
            return;
        }
        else if (isOnSteepSlope && steepSlopeTimer < steepSlopeGracePeriod)
        {
            // 斜坡跳跃 - 只允许在刚接触斜坡时跳跃
            TriggerSlopeJump();
            return;
        }
    }

    private void TriggerSlopeJump()
    {
        // 确保不会重复触发
        if (isPlayingJumpAnimation || pendingJump)
            return;

        // 记录跳跃时间，用于防止连续攀爬
        lastJumpTime = Time.time;

        // 斜坡跳跃不考虑兔子跳机制
        isPendingBunnyHop = false;

        // 这里不需要起跳动画，直接执行跳跃
        PerformJump();
#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("斜坡跳跃: 斜坡角度: {0:F1}°", currentSlopeAngle);
#endif
        jumpBufferCounter = 0; // 重置跳跃缓冲计时器
    }

    // 触发跳跃
    private void TriggerJump()
    {
        // 确保不会重复触发
        if (isPlayingJumpAnimation || pendingJump)
            return;

        // 检查是否可以执行兔子跳（在落地后的短时间窗口内）
        bool canBunnyHop = (Time.time - lastLandingTime) <= bunnyHopWindow;

        // 标记是否为兔子跳
        isPendingBunnyHop = canBunnyHop && Mathf.Abs(lastLandingVelocityX) > 3f;

        // 启动跳跃动画，实际跳跃将在动画完成后执行
        StartJumpAnimation();
        jumpBufferCounter = 0; // 重置跳跃缓冲计时器

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("执行StartJumpAnimation，兔子跳状态: {0}", isPendingBunnyHop);
#endif
    }

    // 处理道具使用
    private void HandleItemUse(bool isRopeBusy)
    {
        if (Input.GetKeyDown(KeyCode.Z) && ropeSystemInitialized && !ropeSystem.IsShooting() && Input.GetKey(KeyCode.LeftShift) == false)
        {
            UseItem();
        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Z) && ropeSystemInitialized && !ropeSystem.IsShooting())
        {
            ForceUseItem();
        }
    }

    // 处理绳索发射
    private void HandleRopeShooting(bool isRopeBusy)
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRopeBusy && CanShootRope && ropeSystemInitialized)
        {
            Vector2 direction = aimIndicator.right * (aimIndicator.localScale.x > 0 ? 1 : -1);
            ropeSystem.ShootRope(direction);
        }
    }


    private void HandleRunningFacing(float horizontalInput)
    {
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            bool shouldFaceRight = horizontalInput > 0;
            SetCharacterFacing(shouldFaceRight);
        }
    }

    private void SetCharacterFacing(bool faceRight)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.flipX = !faceRight; // 默认sprite面向右
        }
    }

    private void StartJumpAnimation()
    {
        // 添加更严格的检查，确保不会重复触发
        if (isPlayingJumpAnimation || pendingJump)
        {
            Debug.LogWarning("尝试在已经播放跳跃动画时再次触发跳跃动画");
            return;
        }

        isPlayingJumpAnimation = true;
        pendingJump = true;

        // 跳跃准备期间可以选择是否禁用水平移动
        CanInput = false;

        // 添加安全机制：即使动画回调失败，也会在短时间后恢复控制
        if (!isRopeMode) { 
        StartCoroutine(JumpAnimationSafetyCheck()); }

        // 添加调试日志，帮助追踪问题
#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.LogFormat("开始跳跃动画，isPlayingJumpAnimation={0}, pendingJump={1}",
                           isPlayingJumpAnimation, pendingJump);
        }
#endif

        playerAnimationController.TriggerJumpAnimation();

#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.LogFormat("广播OnPlayerJump事件，等待跳跃动画完成");
        }
#endif
    }

    // 添加安全检查协程，确保玩家不会永久失去控制
    private IEnumerator JumpAnimationSafetyCheck()
    {
        // 等待足够长的时间，超过正常跳跃动画应该完成的时间
        yield return new WaitForSeconds(playerAnimationController.jumpPrepareAnimDuration+0.1f);

        // 如果跳跃动画状态仍然为播放中，说明可能出现了问题
        if (isPlayingJumpAnimation || pendingJump)
        {
            // 强制恢复控制权和状态
            isPlayingJumpAnimation = false;
            pendingJump = false;
            CanInput = true;

#if UNITY_EDITOR
            if (debugmode)
            {
                Debug.LogWarning("跳跃动画回调未正常触发，通过安全检查强制恢复控制");
            }
#endif
        }
    }

    public void OnJumpAnimationComplete()
    {
#if UNITY_EDITOR
        if (debugmode)
        {
            Debug.Log("Playercontroller收到通知,跳跃动画播放完成，执行实际跳跃");
        }
#endif

        // 确保只有在pendingJump为true时才执行跳跃
        if (pendingJump)
        {
            PerformJump();
            pendingJump = false;
        }
        else
        {
            // 即使没有待处理的跳跃，也确保控制权被恢复
            isPlayingJumpAnimation = false;
            CanInput = true;
        }
    }

    private bool IsMovingAgainstWall()
    {
        // 如果没有水平输入，直接返回false
        if (Mathf.Abs(horizontalInput) < 0.1f)
            return false;

        // 确定检测方向
        Vector2 direction = horizontalInput > 0 ? Vector2.right : Vector2.left;

        // 获取碰撞体尺寸
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        float radius = circleCollider != null ? circleCollider.radius : 0.5f;

        // 从玩家位置发射射线
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, radius + 1f, groundLayers);

        // 可视化射线（仅在编辑器中）
#if UNITY_EDITOR
        if (showGroundCheck)
        {
            Debug.DrawRay(transform.position, direction, hit.collider != null ? Color.green : Color.red);
        }
#endif

        return hit.collider != null;
    }

    private void PerformJump()
    {
        if (!rbInitialized) return;

        // 检查是否正在向墙壁移动
        bool movingAgainstWall = IsMovingAgainstWall();

        // 检查是否在陡峭斜坡上
        bool jumpingOnSteepSlope = isOnSteepSlope;

        // 准备跳跃速度
        float jumpVelocity = initialJumpVelocity;

        // 如果正在向墙壁移动，增加跳跃力
        if (movingAgainstWall)
        {
            jumpVelocity *= 1.3f;
        }

        // 如果在陡峭斜坡上跳跃，减少跳跃力并施加斜坡方向的力
        if (jumpingOnSteepSlope)
        {
            jumpVelocity *= 0.7f; // 减少垂直跳跃力

            // // 应用斜坡方向的力，使玩家更难攀爬陡峭斜坡
            // rb.AddForce(slopeNormalPerp * 3f, ForceMode2D.Impulse);

#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("在陡峭斜坡上跳跃，减少跳跃力并应用斜坡方向力");
#endif
        }

        // 检查是否是兔子跳
        if (isPendingBunnyHop)
        {
            // 计算兔子跳的水平速度
            float momentumRetention = justReleasedFromRope ? 0.98f : bunnyHopMomentumRetention;
            float bunnyHopVelocityX = lastLandingVelocityX * momentumRetention;

            // 使用兔子跳的水平速度和调整后的跳跃高度
            rb.velocity = new Vector2(bunnyHopVelocityX, jumpVelocity);

            // 重置标志
            isPendingBunnyHop = false;
            justReleasedFromRope = false; // 重置绳索脱离标记

#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("执行兔子跳，水平速度: {0}, 垂直速度: {1}", bunnyHopVelocityX, jumpVelocity);
#endif
        }
        else
        {
            // 普通跳跃，保持水平速度不变，使用调整后的跳跃高度
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);

#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("执行普通跳跃，Y轴速度设为: {0}", jumpVelocity);
#endif
        }

        // 如果正在向墙壁移动，在跳跃后应用额外的向上力
        if (movingAgainstWall)
        {
            StartCoroutine(ApplyExtraUpwardForce());
        }

        isJumping = true;
        isPlayingJumpAnimation = false;
        CanInput = true;
    }

    private IEnumerator ApplyExtraUpwardForce()
    {
        // 等待几帧，让初始跳跃速度生效
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // 检查垂直速度，如果被减弱了，应用额外的向上力
        if (rb.velocity.y < initialJumpVelocity * 0.9f)
        {
            // 计算需要补充的力
            float extraForce = initialJumpVelocity * 0.5f;
            rb.AddForce(Vector2.up * extraForce, ForceMode2D.Impulse);

#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("应用额外向上力: {0}", extraForce);
#endif
        }
    }

    private void HandleRopeMode()
    {
        if (!CanInput || !ropeSystemInitialized)
            return;

        bool isPressingUp = Input.GetKey(KeyCode.UpArrow);
        bool isPressingDown = Input.GetKey(KeyCode.DownArrow);
        bool isPressingSpace = Input.GetKeyDown(KeyCode.Space);

        // 处理绳索长度调整
        HandleRopeLengthAdjustment(isPressingUp, isPressingDown);

        // 处理绳索释放
        HandleRopeRelease(isPressingSpace);

        // 处理绳索摆动
        HandleRopeSwinging(horizontalInput);

        // 处理道具使用
        HandleItemUseInRopeMode();

        // 限制最大速度
        LimitMaxVelocity();
    }

    // 处理绳索摆动
    private void HandleRopeSwinging(float horizontalInput)
    {
        float lastSwingTime = 0f;
        float swingInterval = 1f / 60f;
        // 只有当有明显的水平输入时才摆动
        if (Mathf.Abs(horizontalInput) > 0.1f && Time.time >= lastSwingTime + swingInterval)
        {
            // 根据输入方向应用摆动力
            float direction = -Mathf.Sign(horizontalInput); // 反向是因为力的应用方式
            ropeSystem.Swing(direction * swingForce);
            lastSwingTime = Time.time;

#if UNITY_EDITOR
            if (debugmode && Time.frameCount % 30 == 0)
                Debug.LogFormat("绳索摆动: 方向={0}, 力={1}", direction, direction * swingForce);
#endif
        }
    }

    // 处理绳索长度调整
    private void HandleRopeLengthAdjustment(bool isPressingUp, bool isPressingDown)
    {
        // 收缩绳索 - 检查头部是否有障碍物
        if (isPressingUp && !CheckHeadCollision())
        {
            ropeSystem.AdjustRopeLength(-5f);

#if UNITY_EDITOR
            if (debugmode && Time.frameCount % 30 == 0)
                Debug.LogFormat("绳索收缩");
#endif
        }

        // 伸长绳索 - 检查脚下是否有障碍物
        if (isPressingDown && !CheckRopeModeGroundCollision())
        {
            ropeSystem.AdjustRopeLength(5f);

#if UNITY_EDITOR
            if (debugmode && Time.frameCount % 30 == 0)
                Debug.LogFormat("绳索伸长");
#endif
        }
    }

    // 处理绳索释放
    private void HandleRopeRelease(bool isPressingSpace)
    {
        if (isPressingSpace && CanShootRope)
        {
            ropeSystem.ReleaseRope();

#if UNITY_EDITOR
            if (debugmode)
                Debug.LogFormat("释放绳索");
#endif
        }
    }

    // 处理绳索模式下的道具使用
    private void HandleItemUseInRopeMode()
    {
        // 确保绳索不处于发射状态
        if (ropeSystem.IsShooting())
            return;

        // 普通道具使用
        if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftShift) == false)
        {
            UseItem();
        }

        // 强制道具使用
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Z))
        {
            ForceUseItem();
        }
    }

    #endregion

    // 限制最大速度的方法
    private void LimitMaxVelocity()
    {
        if (rbInitialized && rb.velocity.magnitude > maxSwingSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSwingSpeed;
        }
    }

    // 进入绳索模式
    public void EnterRopeMode()
    {
        isRopeMode = true;

        // 隐藏箭头
        if (gunInitialized)
        {
            Gun.SetActive(false);
        }

        // 应用弹性物理材质
        if (bouncyBallMaterial != null && rbInitialized)
        {
            rb.sharedMaterial = bouncyBallMaterial;
        }

        // 确保DistanceJoint2D已启用
        if (distanceJointInitialized)
        {
            distanceJoint.enabled = true;
        }
    }

    // 退出绳索模式
    public void ExitRopeMode()
    {
        if (!isRopeMode)
            return;

        isRopeMode = false;

        // 显示箭头
        if (gunInitialized)
        {
            Gun.SetActive(CanShootRope);
        }

        // 恢复原始物理材质
        if (rbInitialized)
        {
            // 在退出绳索模式时，将当前速度降低到80-90%
            Vector2 currentVelocity = rb.velocity;
            float currentSpeed = currentVelocity.magnitude;

            // 只有当速度足够大时才进行调整
            if (currentSpeed > 12f)
            {
                float speedMultiplier = (currentSpeed > 20f) ? 0.8f : 0.9f;

                // 保持方向不变，但将速度降低
                rb.velocity = currentVelocity.normalized * (currentSpeed * speedMultiplier);

                // 记录脱离绳索时的水平速度，用于后续兔子跳
                lastLandingVelocityX = rb.velocity.x;
                justReleasedFromRope = true;

#if UNITY_EDITOR
                if (debugmode) Debug.LogFormat($"脱离绳索：原速度 {currentSpeed}，调整后速度 {rb.velocity.magnitude}，水平速度 {lastLandingVelocityX}");
#endif
            }

            rb.sharedMaterial = originalMaterial;
        }

        // 禁用DistanceJoint2D
        if (distanceJointInitialized)
        {
            distanceJoint.enabled = false;
        }

        // 重置空中控制时间
        airControlTimeRemaining = airControlDuration;
    }

    // 处理下落时的重力加速
    private void HandleFallingGravity()
    {
        // 只在非绳索模式下处理
        if (!isRopeMode && rbInitialized)
        {
            // 当玩家下落时（Y轴速度为负）
            if (rb.velocity.y < 0)
            {
                // 应用额外的向下力，使下落更快
                rb.AddForce(Vector2.down * fallMultiplier * Physics2D.gravity.magnitude, ForceMode2D.Force);

                // 限制最大下落速度
                if (rb.velocity.y < -maxFallSpeed)
                {
                    rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
                }
            }
        }
    }

    private void HandleEndpointReached(Transform endpointTransform)
    {
        SetPlayerInput(false);
        SetInvincible(true);
        // 如果在绳索模式，强制退出绳索模式
        if (isRopeMode && ropeSystemInitialized)
        {
            ropeSystem.ReleaseRope();
        }

        // 停止所有移动
        if (rbInitialized)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 禁用重力，但不完全冻结玩家，以便我们可以移动它
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 启动协程，平滑移动玩家到终点中心
            StartCoroutine(MovePlayerToEndpoint(endpointTransform));
        }

        // 隐藏箭头
        if (gunInitialized)
        {
            Gun.SetActive(false);
        }


#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("玩家到达终点，开始平滑移动到终点中心");
#endif
    }

    // 平滑移动玩家到终点中心的协程
    private IEnumerator MovePlayerToEndpoint(Transform endpointTransform)
    {
        // 移动持续时间
        float moveDuration = 0.4f;
        float scaleDuration = 0.3f;
        float elapsedTime = 0f;

        // 起始位置和目标位置
        Vector2 startPosition = transform.position;
        Vector3 originalScale = transform.localScale;
        Vector2 targetPosition = endpointTransform.position;
        Vector3 targetScale = Vector3.zero;

        // 第一阶段：移动到终点中心
        while (elapsedTime < moveDuration)
        {
            // 计算已经过的时间比例
            float t = elapsedTime / moveDuration;

            // 应用缓出效果 (Ease Out Cubic)
            float easedT = 1 - Mathf.Pow(1 - t, 3);

            // 插值计算当前位置
            if (rbInitialized)
            {
                rb.position = Vector2.Lerp(startPosition, targetPosition, easedT);
            }
            else
            {
                transform.position = Vector2.Lerp(startPosition, targetPosition, easedT);
            }

            // 增加已经过的时间
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        // 确保位置精确
        if (rbInitialized)
        {
            rb.position = targetPosition;
        }
        else
        {
            transform.position = targetPosition;
        }

        // 第二阶段：缩小至消失
        elapsedTime = 0f;
        while (elapsedTime < scaleDuration)
        {
            // 计算已经过的时间比例
            float t = elapsedTime / scaleDuration;

            // 应用缓出效果 (Ease Out Cubic)
            float easedT = 1 - Mathf.Pow(1 - t, 3);

            // 插值计算当前缩放
            transform.localScale = Vector3.Lerp(originalScale, targetScale, easedT);

            // 增加已经过的时间
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        // 确保最终缩放为0
        transform.localScale = targetScale;

        // 移动完成后完全冻结玩家
        if (rbInitialized)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("玩家已先移动到终点中心，然后缩小至0");
#endif

        // 在移动完成后触发事件，通知场景管理器可以开始加载目标场景了
        GameEvents.TriggerPlayerReachedEndpointCenter(endpointTransform);
    }


    // 修改UseItem方法，使用新的交互系统
    public void UseItem()
    {
        // 调用交互事件并获取返回值
        bool handled = GameEvents.TriggerPlayerInteract(currentInteractionType);

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("使用道具或交互，交互类型: {0}，是否被处理: {1}", currentInteractionType, handled);
#endif
    }

    public void ForceUseItem()
    {
        // 强制使用Item类型交互，无论当前交互类型是什么
        bool handled = GameEvents.TriggerPlayerInteract(GameEvents.InteractionType.Item);

#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("强制使用道具交互，是否被处理: {0}", handled);
#endif
    }

    // 提供给其他脚本访问玩家状态的方法
    public bool IsInRopeMode()
    {
        return isRopeMode;
    }

    public Rigidbody2D GetRigidbody()
    {
        return rb;
    }

    // 获取DistanceJoint2D组件
    public DistanceJoint2D GetDistanceJoint()
    {
        return distanceJoint;
    }

    #region OnCollision
    // 碰撞检测
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果处于无敌状态，直接返回
        if (isInvincible) return;

        // 检查是否碰到冰面
        if (collision.gameObject.CompareTag("Ice") && !isIceImmune)
        {
            GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Frozen);
        }
        // 检查是否碰到火焰
        else if (collision.gameObject.CompareTag("Fire") && !isFireImmune)
        {
            GameEvents.TriggerSetPlayerBurning(true);
            GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Burning);
        }
        // 是否被电击
        else if (collision.gameObject.CompareTag("Elect") && !isElectricImmune)
        {
            GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Electrified);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // 检查是否离开火焰
        if (collision.gameObject.CompareTag("Fire"))
        {
            // 当玩家离开火物体时，设置 isPlayerBurn 为 false
            // 但不要改变状态，让状态管理器处理持续燃烧效果
            GameEvents.TriggerSetPlayerBurning(false);
        }
    }

    // 性能优化：使用缓存的位置进行预测性碰撞检测
    public void CheckPredictiveElementalCollision(Vector2 currentPos, Vector2 predictedPos, LayerMask collisionLayers)
    {
        // 如果处于无敌状态，直接返回
        if (isInvincible) return;

        // 创建射线，从当前位置到预测位置
        RaycastHit2D hit = Physics2D.Linecast(currentPos, predictedPos, collisionLayers);

        // 如果检测到碰撞
        if (hit.collider != null)
        {
            // 检查碰撞物体的标签
            string hitTag = hit.collider.tag;

            // 根据标签触发相应状态
            if (hitTag == "Ice" && !isIceImmune)
            {
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Frozen);
            }
            else if (hitTag == "Fire" && !isFireImmune)
            {
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Burning);
            }
            else if (hitTag == "Electric" && !isElectricImmune)
            {
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Electrified);
            }
        }
    }
    #endregion

    #region HandlePlayerDeadth
    private void HandlePlayerDied()
    {
        SetPlayerInput(false);
        // 如果在绳索模式，强制退出绳索模式
        if (isRopeMode && ropeSystemInitialized)
        {
            ropeSystem.ReleaseRope();
        }

        // 停止所有移动
        if (rbInitialized)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 可选：禁用重力，完全冻结玩家
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        // 隐藏箭头
        if (gunInitialized)
        {
            Gun.SetActive(false);
        }



#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("玩家死亡，已禁用所有输入和移动");
#endif
    }

    private void HandlePlayerRespawn()
    {
        SetPlayerInput(true);
        ResetAllImmunities();
        ResetJumpState();
        if (rbInitialized)
        {
            rb.gravityScale = originalgravityscale;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        isJumping = false;
    }
    #endregion

    #region PlayerControll Switch
    public void SetPlayerInput(bool Input)
    {
        SetPlayerMove(Input);
        HandleCanShootRopeChanged(Input);
    }

    public void SetPlayerMove(bool canInput)
    {
        CanInput = canInput;
    }

    public void HandleCanShootRopeChanged(bool canShoot)
    {
        CanShootRope = canShoot;

        // 更新箭头显示状态 - 修改这里的逻辑
        if (gunInitialized)
        {
            // 只有在以下所有条件都满足时才显示Gun:
            // 1. 可以发射绳索
            // 2. 不在绳索模式
            // 3. 绳索系统已初始化
            // 4. 绳索不在发射或钩住状态
            bool shouldShowGun = canShoot && !isRopeMode && ropeSystemInitialized && !ropeSystem.IsShooting() && !ropeSystem.IsRopeShootingOrHooked();

            // 只在状态变化时更新Gun的显示状态，避免不必要的SetActive调用
            if (Gun.activeSelf != shouldShowGun)
            {
                Gun.SetActive(shouldShowGun);
            }
        }
    }

    #endregion

    #region 公共方法
    public void ShowPlayerArrow()
    {
        if (gunInitialized)
        {
            Gun.SetActive(false);
        }
    }

    public bool isPlayerAiming()
    {
        return isAiming;
    }

    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public bool isPlayerGrounded()
    {
        return isGrounded;
    }

    public bool isPlayerRopeMode()
    {
        return isRopeMode;
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    // 性能优化：缓存检测结果
    private bool cachedElectrifiedState = false;
    private float lastElectrifiedCheckTime = 0f;
    private float electricCheckInterval = 0.2f; // 每0.2秒检测一次

    public bool IsHookingElectrifiedObject()
    {
        // 性能优化：使用缓存的结果，如果时间间隔不够
        if (Time.time < lastElectrifiedCheckTime + electricCheckInterval)
        {
            return cachedElectrifiedState;
        }

        lastElectrifiedCheckTime = Time.time;

        // 首先检查是否在绳索模式
        if (!isRopeMode || !ropeSystemInitialized || !ropeSystem.HasAnchors())
        {
            cachedElectrifiedState = false;
            return false;
        }

        // 获取当前钩中的锚点位置
        Vector2 anchorPosition = ropeSystem.GetCurrentAnchorPosition();

        // 检查锚点位置是否有带电物体
        Collider2D[] colliders = Physics2D.OverlapCircleAll(anchorPosition, 0.1f);

        foreach (Collider2D collider in colliders)
        {
            // 检查碰撞体是否带有"Elect"标签
            if (collider.CompareTag("Elect"))
            {
                cachedElectrifiedState = true;
                return true;
            }
        }

        // 性能优化：减少射线检测的频率
        // 每三次检测才进行一次完整的绳索路径检测
        if (Time.frameCount % 3 == 0)
        {
            // 检查绳索路径上是否有带电物体
            Vector2 playerPosition = transform.position;
            RaycastHit2D[] hits = Physics2D.LinecastAll(playerPosition, anchorPosition);

            foreach (RaycastHit2D hit in hits)
            {
                // 检查碰撞体是否带有"Electric"或"Elect"标签
                if (hit.collider != null && hit.collider.CompareTag("Elect"))
                {
                    cachedElectrifiedState = true;
                    return true;
                }
            }
        }

        cachedElectrifiedState = false;
        return false;
    }

    // 性能优化：合并免疫效果管理
    public void SetInvincible(bool invincible, float duration = 0f)
    {
        isInvincible = invincible;

        // 应用无敌视觉效果
        if (playerRendererInitialized)
        {
            playerRenderer.color = invincible ? invincibleTint : originalColor;
        }

        // 激活/关闭无敌特效
        if (invincibleEffectInitialized)
        {
            invincibleEffect.SetActive(invincible);
        }

        // 如果设置了持续时间，启动协程
        if (invincible && duration > 0f)
        {
            // 停止之前的协程
            if (immunityCoroutines[0] != null)
            {
                StopCoroutine(immunityCoroutines[0]);
            }
            immunityCoroutines[0] = StartCoroutine(DisableImmunityAfterDelay(0, duration));
        }
    }

    /// 设置冰免疫
    public void SetIceImmunity(bool immune, float duration = 0f)
    {
        isIceImmune = immune;

        // 如果设置了持续时间，启动协程
        if (immune && duration > 0f)
        {
            // 停止之前的协程
            if (immunityCoroutines[1] != null)
            {
                StopCoroutine(immunityCoroutines[1]);
            }
            immunityCoroutines[1] = StartCoroutine(DisableImmunityAfterDelay(1, duration));
        }
    }

    /// 设置火免疫
    public void SetFireImmunity(bool immune, float duration = 0f)
    {
        isFireImmune = immune;

        // 如果设置了持续时间，启动协程
        if (immune && duration > 0f)
        {
            // 停止之前的协程
            if (immunityCoroutines[2] != null)
            {
                StopCoroutine(immunityCoroutines[2]);
            }
            immunityCoroutines[2] = StartCoroutine(DisableImmunityAfterDelay(2, duration));
        }
    }

    /// 设置电免疫
    public void SetElectricImmunity(bool immune, float duration = 0f)
    {
        isElectricImmune = immune;

        // 如果设置了持续时间，启动协程
        if (immune && duration > 0f)
        {
            // 停止之前的协程
            if (immunityCoroutines[3] != null)
            {
                StopCoroutine(immunityCoroutines[3]);
            }
            immunityCoroutines[3] = StartCoroutine(DisableImmunityAfterDelay(3, duration));
        }
    }

    /// 设置全元素免疫（但不是无敌）
    public void SetAllElementalImmunity(bool immune, float duration = 0f)
    {
        isIceImmune = immune;
        isFireImmune = immune;
        isElectricImmune = immune;

        // 如果设置了持续时间，启动协程
        if (immune && duration > 0f)
        {
            // 停止之前的所有元素免疫协程
            for (int i = 1; i <= 3; i++)
            {
                if (immunityCoroutines[i] != null)
                {
                    StopCoroutine(immunityCoroutines[i]);
                    immunityCoroutines[i] = null;
                }
            }

            // 启动新的协程
            for (int i = 1; i <= 3; i++)
            {
                immunityCoroutines[i] = StartCoroutine(DisableImmunityAfterDelay(i, duration));
            }
        }
    }

    private void HandleStoryModeChanged(bool isInStoryMode)
    {
        // 在故事模式下禁止玩家控制
        SetPlayerInput(!isInStoryMode);
    }

    // 添加处理玩家进入/离开交互区域的方法

    private GameEvents.InteractionType currentInteractionType = GameEvents.InteractionType.Item; // 默认为道具交互
    private void HandleInteractiveZoneChanged(bool inZone, GameEvents.InteractionType interactionType)
    {
        if (inZone)
        {
            // 玩家进入交互区域，更新当前交互类型
            currentInteractionType = interactionType;
        }
        else
        {
            // 玩家离开交互区域，恢复默认交互类型
            currentInteractionType = GameEvents.InteractionType.Item;
        }
    }

    /// 获取无敌状态
    public bool IsInvincible()
    {
        return isInvincible;
    }

    /// 获取冰免疫状态
    public bool IsIceImmune()
    {
        return isIceImmune || isInvincible; // 无敌状态也包含冰免疫
    }

    /// 获取火免疫状态
    public bool IsFireImmune()
    {
        return isFireImmune || isInvincible; // 无敌状态也包含火免疫
    }

    /// 获取电免疫状态
    public bool IsElectricImmune()
    {
        return isElectricImmune || isInvincible; // 无敌状态也包含电免疫
    }

    public void ResetAllImmunities()
    {
        isIceImmune = false;
        isFireImmune = false;
        isElectricImmune = false;
        isInvincible = false;

        // 停止所有免疫协程
        for (int i = 0; i < immunityCoroutines.Length; i++)
        {
            if (immunityCoroutines[i] != null)
            {
                StopCoroutine(immunityCoroutines[i]);
                immunityCoroutines[i] = null;
            }
        }

        // 重置视觉效果
        if (playerRendererInitialized)
        {
            playerRenderer.color = originalColor;
        }

        if (invincibleEffectInitialized)
        {
            invincibleEffect.SetActive(false);
        }
    }
    public void ResetJumpState()
    {
        // 重置跳跃相关变量
        isJumping = false;
        jumpBufferCounter = 0;

        // 确保跳跃冷却已重置
        isPlayingJumpAnimation = false;

        // 记录日志以便调试
#if UNITY_EDITOR
        if (debugmode)
            Debug.LogFormat("跳跃状态已重置");
#endif
    }

    #endregion

    #region 免疫协程
    // 性能优化：合并所有免疫协程为一个方法，使用索引区分
    private IEnumerator DisableImmunityAfterDelay(int immunityType, float delay)
    {
        // 使用缓存的WaitForSeconds对象
        yield return new WaitForSeconds(delay);

        // 根据免疫类型禁用相应的免疫
        switch (immunityType)
        {
            case 0: // 无敌
                SetInvincible(false);
                break;
            case 1: // 冰免疫
                isIceImmune = false;
                break;
            case 2: // 火免疫
                isFireImmune = false;
                break;
            case 3: // 电免疫
                isElectricImmune = false;
                break;
        }

        // 清除协程引用
        immunityCoroutines[immunityType] = null;
    }
    #endregion

    #region Event methods
    public void OnEnable()
    {
        GameEvents.OnCanShootRopeChanged += HandleCanShootRopeChanged;
        GameEvents.OnPlayerDied += HandlePlayerDied;
        GameEvents.OnPlayerRespawnCompleted += HandlePlayerRespawn;
        GameEvents.OnPlayerInInteractiveZoneChanged += HandleInteractiveZoneChanged;
        GameEvents.OnEndpointReached += HandleEndpointReached;
        GameEvents.OnStoryModeChanged += HandleStoryModeChanged;
    }

    private void OnDisable()
    {
        // 移除事件监听
        GameEvents.OnCanShootRopeChanged -= HandleCanShootRopeChanged;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
        GameEvents.OnPlayerRespawnCompleted -= HandlePlayerRespawn;
        GameEvents.OnPlayerInInteractiveZoneChanged -= HandleInteractiveZoneChanged;
        GameEvents.OnEndpointReached -= HandleEndpointReached;
        GameEvents.OnStoryModeChanged -= HandleStoryModeChanged;

        // 停止所有协程
        StopAllCoroutines();
    }
    #endregion


#if UNITY_EDITOR
    #region DrawGizmos
    private void OnDrawGizmos()
    {
        // 如果不启用边缘辅助，不绘制调试信息
        if (!enableEdgeAssist || !debugmode)
            return;

        // 绘制边缘检测范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position,
            transform.position + new Vector3(edgeAssistCheckDistance, 0, 0)
        );
        Gizmos.DrawLine(
            transform.position,
            transform.position + new Vector3(-edgeAssistCheckDistance, 0, 0)
        );

        // 如果正在执行边缘辅助，绘制目标位置
        if (isPerformingEdgeAssist)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(edgeAssistTargetPosition, 0.1f);
            Gizmos.DrawLine(transform.position, edgeAssistTargetPosition);
        }
    }
    #endregion
#endif
}


