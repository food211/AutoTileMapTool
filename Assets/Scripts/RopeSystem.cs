using System.Collections.Generic;
using UnityEngine;

public class RopeSystem : MonoBehaviour
{
    [Header("钩索设置")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float segmentLength = 0.25f;
    [SerializeField] private LayerMask hookableLayerMask;
    [SerializeField] private float maxRopeLength = 20f;
    [SerializeField] private float minRopeLength = 2f;
    [SerializeField] private float ropeAdjustSpeed = 2f; // 绳索调整速度
    
    [Header("物理设置")]
    [SerializeField] private int constraintIterations = 50;
    [SerializeField] private float ropeWidth = 0.1f;
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
    private List<Vector2> ropePositions = new List<Vector2>();
    private Vector2 lastPlayerPosition;
    
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
                // 不显示绳索
                lineRenderer.positionCount = 0;
                break;
                
            case RopeState.Shooting:
                UpdateRopeShooting();
                break;
                
            case RopeState.Hooked:
                HandleRopeInput();
                SimulateRope();
                break;
        }
    }
    
    private void HandleRopeInput()
    {
        // 处理钩索模式下的输入
        // 这部分可以移到PlayerController中，这里仅作为示例
        
        // 上键缩短绳索
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            AdjustRopeLength(-ropeAdjustSpeed * Time.deltaTime);
        }
        
        // 下键延长绳索
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            AdjustRopeLength(ropeAdjustSpeed * Time.deltaTime);
        }
        
        // 空格键释放绳索
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ReleaseRope();
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
        maxRopeLength = maxLength;
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
        
        // 初始化绳索段
        InitializeRopeSegments();
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
        if (distanceToTravel >= maxRopeLength)
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
        
        // 初始化所有绳索段在同一位置
        Vector2 segmentPos = transform.position;
        for (int i = 0; i < segmentCount; i++)
        {
            ropeSegments.Add(new RopeSegment(segmentPos));
        }
    }
    
    private void SimulateRope()
    {
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
        // 应用距离约束，保持绳索段之间的距离
        for (int i = 0; i < ropeSegments.Count - 1; i++)
        {
            RopeSegment firstSeg = ropeSegments[i];
            RopeSegment secondSeg = ropeSegments[i + 1];
            
            float dist = Vector2.Distance(firstSeg.posNow, secondSeg.posNow);
            float error = dist - segmentLength;
            
            Vector2 changeDir = (firstSeg.posNow - secondSeg.posNow).normalized;
            Vector2 changeAmount = changeDir * error;
            
            if (i > 0)
            {
                firstSeg.posNow -= changeAmount * 0.5f;
                ropeSegments[i] = firstSeg;
                secondSeg.posNow += changeAmount * 0.5f;
                ropeSegments[i + 1] = secondSeg;
            }
            else
            {
                // 第一段固定在玩家位置
                secondSeg.posNow += changeAmount;
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
        if (currentState != RopeState.Hooked)
            return;
            
        Rigidbody2D playerRb = playerController.GetComponent<Rigidbody2D>();
        if (playerRb == null)
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
        if (!isRopeActive)
            return;
            
        Rigidbody2D playerRb = playerController.GetComponent<Rigidbody2D>();
        if (playerRb == null)
            return;
            
        // 计算垂直于绳索的方向
        Vector2 ropeDir = (hookPosition - transform.position).normalized;
        Vector2 perpDir = new Vector2(-ropeDir.y, ropeDir.x);
        
        // 应用摆动力
        playerRb.AddForce(perpDir * direction * 10f);
    }
    
    public void AdjustRopeLength(float amount)
    {
        if (!isRopeActive)
            return;
            
        float newRopeLength = currentRopeLength + amount;
        
        // 检查是否超出最大长度限制
        if (newRopeLength > maxRopeLength)
        {
            newRopeLength = maxRopeLength;
        }
        // 检查是否小于最小长度限制
        else if (newRopeLength < minRopeLength)
        {
            newRopeLength = minRopeLength;
        }
        
        // 如果是缩短绳索，检查是否会与障碍物碰撞
        if (amount < 0)
        {
            // 计算玩家到钩子的方向
            Vector2 dirToHook = ((Vector2)hookPosition - (Vector2)transform.position).normalized;
            
            // 检测玩家和钩子之间是否有障碍物
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                dirToHook,
                newRopeLength,
                obstacleLayerMask
            );
            
            if (hit.collider != null)
            {
                // 如果有障碍物，限制绳索长度为玩家到障碍物的距离
                float distanceToObstacle = hit.distance;
                newRopeLength = Mathf.Max(distanceToObstacle, minRopeLength);
            }
        }
        
        // 更新绳索长度
        currentRopeLength = newRopeLength;
        
        // 重新计算绳索段长度
        segmentLength = currentRopeLength / (segmentCount - 1);
    }
    
    public void ReleaseRope()
    {
        currentState = RopeState.Idle;
        isRopeActive = false;
        
        if (hookInstance != null)
            hookInstance.gameObject.SetActive(false);
            
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