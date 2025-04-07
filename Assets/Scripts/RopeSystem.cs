using System.Collections.Generic;
using UnityEngine;

public class RopeSystem : MonoBehaviour
{
    [Header("钩索设置")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float segmentLength = 0.25f;
    [SerializeField] private LayerMask hookableLayerMask;
    [SerializeField] private float maxRopeLength = 100f;
    [SerializeField] private float minRopeLength = 2f;

    
    [Header("物理设置")]
    [SerializeField] private int constraintIterations = 50;
    [SerializeField] private float ropeWidth = 0.8f;
    [SerializeField] private bool useGravity = true;  // 是否使用重力
    [SerializeField] private float gravityScale = 0.5f;  // 重力系数
    [SerializeField] private LayerMask obstacleLayerMask; // 障碍物层级
    
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform hookPrefab;
    
    // 内部变量
    private LineRenderer lineRenderer;
    private List<RopeSegment> ropeSegments = new List<RopeSegment>();
    private bool isRopeActive = false; // 是否激活钩索模式
    private Vector3 hookPosition;
    private Transform hookInstance;
    private float currentRopeLength;
    private Rigidbody2D playerRb; // 缓存的玩家刚体组件
    
    // 钩索状态
    private enum RopeState { Idle, Shooting, Hooked }
    private RopeState currentState = RopeState.Idle;
    private float shootTimer = 0f;
    private Vector2 shootDirection;
    private float shootSpeed;
    
    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            
        SetupLineRenderer();
        
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
            playerRb = playerController.GetComponent<Rigidbody2D>();         
    }
    
    private void SetupLineRenderer()
    {
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        
        // 可以设置材质等其他属性
    }
    
private void Update()
{
    switch (currentState)
    {
        case RopeState.Idle:
            lineRenderer.positionCount = 0;
            break;

        case RopeState.Shooting:
            UpdateRopeShooting();
            break;

        case RopeState.Hooked:
            if (ropeSegments == null || ropeSegments.Count == 0)
            {
                InitializeRopeSegments(); // 确保绳子段被正确初始化
            }
            SimulateRope();
            break;
    }
}

    
    public void ShootRope(Vector2 direction, float speed, float maxLength)
    {
        if (currentState != RopeState.Idle)
            return;
            
        // 初始化发射状态
        shootDirection = direction.normalized;
        shootSpeed = speed;
        currentRopeLength = 0f;
        shootTimer = 0f;
        isRopeActive = false; // 发射状态下还未激活钩索模式
        
        // 创建钩子
        if (hookInstance == null && hookPrefab != null)
        {
            hookInstance = Instantiate(hookPrefab, transform.position, Quaternion.identity);
        }
        else if (hookInstance != null)
        {
            hookInstance.position = transform.position;
            hookInstance.gameObject.SetActive(true);
        }
        
        currentState = RopeState.Shooting;
    }
    
    private void UpdateRopeShooting()
    {
        shootTimer += Time.deltaTime;
        
        // 计算当前钩子位置
        float distanceToTravel = shootTimer * shootSpeed;
        Vector2 hookTip = (Vector2)transform.position + shootDirection * distanceToTravel;
        
        if (hookInstance != null)
        {
            hookInstance.position = hookTip;
            hookInstance.rotation = Quaternion.LookRotation(Vector3.forward, Quaternion.Euler(0, 0, 90) * shootDirection);
        }
        
        // 检测是否击中物体
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, 
            shootDirection, 
            distanceToTravel,
            hookableLayerMask
        );
        
        if (hit.collider != null)
        {
            // 击中物体，切换到钩索模式
            hookPosition = hit.point;
            if (hookInstance != null)
                hookInstance.position = hookPosition;
                
            currentState = RopeState.Hooked;
            currentRopeLength = Vector2.Distance(transform.position, hookPosition);
            isRopeActive = true; // 激活钩索模式
            
            // 通知玩家控制器进入钩索模式
            playerController.EnterRopeMode();
            return;
        }
        
        // 检查是否达到最大长度
        if (distanceToTravel >= playerController.GetMaxShootRopeLength())
        {
            // 收回钩索
            ReleaseRope();
        }
        
        // 更新绳索渲染
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, hookTip);
    }
    
    private void InitializeRopeSegments()
    {
        ropeSegments.Clear();

        // 初始化所有绳索段在从玩家到钩子之间的均匀分布
        Vector2 startPos = transform.position;
        Vector2 endPos = hookPosition; // 假设钩子已经击中目标
        Vector2 segmentDelta = (endPos - startPos) / (segmentCount - 1);

        for (int i = 0; i < segmentCount; i++)
        {
            Vector2 segmentPos = startPos + segmentDelta * i;
            ropeSegments.Add(new RopeSegment(segmentPos));
        }
    }

    
    private void SimulateRope()
    {
        if (ropeSegments == null || ropeSegments.Count < 2)
        {
            Debug.LogError("RopeSegments not initialized or insufficient segments.");
            return; // 防止越界错误
        }
        // 应用物理约束
        Vector2 playerPos = transform.position;
        
        // 第一个点固定在玩家位置
        RopeSegment firstSegment = ropeSegments[0];
        firstSegment.posNow = playerPos;
        ropeSegments[0] = firstSegment;
        
        // 最后一个点固定在钩子位置
        RopeSegment lastSegment = ropeSegments[ropeSegments.Count - 1];
        lastSegment.posNow = hookPosition;
        ropeSegments[ropeSegments.Count - 1] = lastSegment;
        
        // 性能优化：只有在启用重力时才处理中间段
        if (useGravity)
        {
            // 应用重力
            Vector2 gravity = Physics2D.gravity * gravityScale * Time.deltaTime * Time.deltaTime;
            
            for (int i = 1; i < ropeSegments.Count - 1; i++)
            {
                RopeSegment segment = ropeSegments[i];
                
                // 存储旧位置
                segment.posOld = segment.posNow;
                
                // 应用重力
                segment.posNow += gravity;
                
                ropeSegments[i] = segment;
            }
        }
        
        // 应用距离约束
        for (int i = 0; i < constraintIterations; i++)
        {
            ApplyConstraints();
        }
        
        // 检测绳索与环境的碰撞
        CheckRopeCollision();
        
        // 更新线渲染器
        UpdateLineRenderer();
        
        // 计算绳索对玩家的影响
        ApplyRopeForceToPlayer();
    }
    
   private void ApplyConstraints()
{
    for (int i = 0; i < ropeSegments.Count - 1; i++)
    {
        RopeSegment firstSeg = ropeSegments[i];
        RopeSegment secondSeg = ropeSegments[i + 1];

        float dist = Vector2.Distance(firstSeg.posNow, secondSeg.posNow);
        float error = dist - segmentLength;

        Vector2 changeDir = (firstSeg.posNow - secondSeg.posNow).normalized;
        Vector2 changeAmount = changeDir * Mathf.Clamp(error, -segmentLength * 1.2f, segmentLength * 1.2f);

        // 减小调整幅度，让绳子更像刚体
        float stiffnessFactor = 0.8f; // 刚性比例，值越接近1越刚性

        if (i == 0)
        {
            // 第一个点保持不变（跟随玩家位置）
            firstSeg.posNow = transform.position; // RopeSystem挂在玩家子层级上，玩家位置即为第一个点
            ropeSegments[i] = firstSeg;

            // 第二段调整
            secondSeg.posNow += changeAmount * stiffnessFactor;
            ropeSegments[i + 1] = secondSeg;
        }
        else if (i == ropeSegments.Count - 2)
        {
            // 最后一个点固定在钩子位置
            RopeSegment hookSegment = ropeSegments[i + 1];
            hookSegment.posNow = hookPosition;
            ropeSegments[i + 1] = hookSegment;

            // 倒数第二段调整
            firstSeg.posNow -= changeAmount * stiffnessFactor;
            ropeSegments[i] = firstSeg;
        }
        else
        {
            // 中间点正常调整
            firstSeg.posNow -= changeAmount * stiffnessFactor * 0.5f;
            ropeSegments[i] = firstSeg;

            secondSeg.posNow += changeAmount * stiffnessFactor * 0.5f;
            ropeSegments[i + 1] = secondSeg;
        }
    }
}

    
    private void CheckRopeCollision()
    {
        // 简化的碰撞检测
        for (int i = 1; i < ropeSegments.Count - 1; i++)
        {
            Vector2 segmentPos = ropeSegments[i].posNow;
            
            RaycastHit2D hit = Physics2D.CircleCast(
                segmentPos,
                ropeWidth / 2,
                Vector2.zero,
                0.1f,
                hookableLayerMask
            );
            
            if (hit.collider != null)
            {
                // 处理碰撞，可以让绳索绕过障碍物
                Vector2 collisionNormal = hit.normal;
                RopeSegment segment = ropeSegments[i];
                segment.posNow = hit.point + collisionNormal * (ropeWidth / 2);
                ropeSegments[i] = segment;
            }
        }
    }
    
    private void UpdateLineRenderer()
    {
        lineRenderer.positionCount = ropeSegments.Count;
        
        for (int i = 0; i < ropeSegments.Count; i++)
        {
            lineRenderer.SetPosition(i, ropeSegments[i].posNow);
        }
    }
    
    private void ApplyRopeForceToPlayer()
    {
        if (currentState != RopeState.Hooked || playerRb == null)
            return;
            
        // 计算绳索方向
        Vector2 ropeDir = (ropeSegments[1].posNow - (Vector2)transform.position).normalized;
        
        // 限制玩家不会超出绳索长度
        float distanceToSecondSegment = Vector2.Distance(transform.position, ropeSegments[1].posNow);
        if (distanceToSecondSegment > segmentLength)
        {
            // 计算玩家应该在的位置
            Vector2 constrainedPos = ropeSegments[1].posNow - ropeDir * segmentLength;
            
            // 应用力而不是直接设置位置，使运动更平滑
            Vector2 forceDir = constrainedPos - (Vector2)transform.position;
            playerRb.AddForce(forceDir * 50f);
        }
    }
    
    public void Swing(float direction)
    {
        if (currentState != RopeState.Hooked || playerRb == null)
            return;
            
        // 计算垂直于绳索的方向
        Vector2 ropeDir = (hookPosition - transform.position).normalized;
        Vector2 perpDir = new Vector2(-ropeDir.y, ropeDir.x);
        
        // 应用摆动力
        playerRb.AddForce(perpDir * direction * 10f);
    }
    
    public void AdjustRopeLength(float amount)
{
    if (currentState != RopeState.Hooked)
        return;

    // 计算新的绳索长度
    float newRopeLength = currentRopeLength + amount;

    // 检查是否超出最大或最小长度限制
    if (newRopeLength > maxRopeLength)
    {
        newRopeLength = maxRopeLength;
    }
    else if (newRopeLength < minRopeLength)
    {
        newRopeLength = minRopeLength;
    }

    // 计算需要的绳节数量
    int newSegmentCount = Mathf.RoundToInt(newRopeLength / segmentLength);

    // 确保绳节数量至少为2（玩家和钩子位置）
    newSegmentCount = Mathf.Max(newSegmentCount, 2);

    // 如果绳节数量发生变化，调整绳节列表
    if (newSegmentCount != ropeSegments.Count)
    {
        List<RopeSegment> newSegments = new List<RopeSegment>();

        Vector2 startPos = transform.position;
        Vector2 endPos = hookPosition;

        // 重新生成绳节
        Vector2 segmentDelta = (endPos - startPos) / (newSegmentCount - 1);
        for (int i = 0; i < newSegmentCount; i++)
        {
            Vector2 segmentPos = startPos + segmentDelta * i;
            newSegments.Add(new RopeSegment(segmentPos));
        }

        ropeSegments = newSegments;
    }

    // 更新当前绳索长度
    currentRopeLength = newRopeLength;

    // 更新绳索物理状态
    SimulateRope();
}
    
    public void ReleaseRope()
{
    // 切换到空闲状态
    currentState = RopeState.Idle;
    isRopeActive = false;

    // 清除钩子实例
    if (hookInstance != null)
    {
        hookInstance.gameObject.SetActive(false);
    }

    // 清除绳索段信息
    ropeSegments.Clear();

    // 重置线渲染器
    lineRenderer.positionCount = 0;

    // 通知玩家控制器退出钩索模式
    playerController.ExitRopeMode();
}

    // 设置是否使用重力
    public void SetGravity(bool enabled)
    {
        useGravity = enabled;
    }
    
    // 设置重力系数
    public void SetGravityScale(float scale)
    {
        gravityScale = scale;
    }
    
    // 检查是否处于钩索模式
    public bool IsRopeActive()
    {
        return isRopeActive;
    }
    public bool IsRopeShootingOrHooked()
    {
        return currentState == RopeState.Shooting || currentState == RopeState.Hooked;
    }


    public Vector3 GetHookPosition()
    {
        return hookPosition;
    }
    
    // 绳索段类
    private struct RopeSegment
    {
        public Vector2 posNow;
        public Vector2 posOld;
        
        public RopeSegment(Vector2 pos)
        {
            posNow = pos;
            posOld = pos;
        }
    }
}