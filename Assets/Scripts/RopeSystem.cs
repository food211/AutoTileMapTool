using System.Collections.Generic;
using UnityEngine;

public class RopeSystem : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private StatusManager statusManager;
    [SerializeField] private GameObject arrowPrefab; // 箭头预制体引用
    
    [Header("绳索设置")]
    [SerializeField] private float ropeLength = 50f;
    [SerializeField] private float ropeAdjustSpeed = 2f;
    [SerializeField] private float minRopeLength = 1f;
    [SerializeField] private float maxRopeLength = 100f;
    [SerializeField] private float ropeShootSpeed = 50f; // 发射速度
    [Header("绳索延迟断开")]
    [SerializeField] private float maxLengthHoldTime = 0.5f; // 绳索达到最大长度后保持的时间
    private float maxLengthTimer = 0f; // 计时器
    private bool isAtMaxLength = false; // 是否已达到最大长度
    private Color originalRopeColor;
    private float originalStartWidth;
    private float originalEndWidth;
    private AnimationCurve originalWithCurve;
    private Gradient originalColorGradient;
    
    [Header("碰撞检测")]
    [SerializeField] private LayerMask hookableLayers; // 可以被钩住的层
    [SerializeField] private LayerMask collisionOnlyLayers; // 可以碰撞但不可钩住的层
    [SerializeField] private float linecastOffset = 0.1f; // 增加偏移量，从0.01f改为0.1f
    [SerializeField] private float anchorSafetyCheck = 0.15f; // 锚点安全检查距离
    [SerializeField] private int swingCollisionSteps = 25;
    [Header("燃烧效果设置")]
    [SerializeField] private float burnPropagationSpeed = 0.25f; // 燃烧传播速度
    [SerializeField] private float burnBreakThreshold = 0.8f; // 燃烧到多少程度时绳索断开
    [SerializeField] private GameObject fireParticlePrefab; // 火焰粒子效果预制体

    // 燃烧状态相关变量
    private int burningAnchorIndex = -1; // 开始燃烧的锚点索引
    private GameObject fireParticleInstance; // 火焰粒子实例
    
    // 内部变量
    private bool isShooting = false;
    private bool isHooked = false;
    private Vector2 hookPosition;
    private Vector2 shootDirection;
    private float shootDistance = 0f;
    private float currentRopeLength;
    private Rigidbody2D playerRigidbody;
    private GameObject arrowObject; // 箭头对象
    private SpriteRenderer arrowRenderer;
    private bool isFacingLeft = false; // 玩家朝向
    
    // 当前钩中的物体标签
    private string currentHookTag = "";
    // 特效协程引用

    // 绳索弯曲相关
    private List<Vector2> anchors = new List<Vector2>();
    private float combinedAnchorLen = 0f;
    private DistanceJoint2D distanceJoint;
    
    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
            
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
            
        playerRigidbody = playerController.GetComponent<Rigidbody2D>();
        
        // 获取或添加DistanceJoint2D组件
        distanceJoint = playerController.GetComponent<DistanceJoint2D>();
        if (distanceJoint == null)
        {
            distanceJoint = playerController.gameObject.AddComponent<DistanceJoint2D>();
            distanceJoint.enabled = false;
            distanceJoint.autoConfigureDistance = false;
            distanceJoint.enableCollision = true;
        }

        // 保存原始绳索颜色
        originalRopeColor = lineRenderer.startColor;
        originalStartWidth = lineRenderer.startWidth;
        originalEndWidth = lineRenderer.endWidth;
        originalWithCurve = lineRenderer.widthCurve;
        originalColorGradient = lineRenderer.colorGradient;

        // 初始化线渲染器
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        
        // 如果没有设置可钩层，默认设置为Ground层
        if (hookableLayers.value == 0)
            hookableLayers = LayerMask.GetMask("Ground");
            
        // 在初始化时创建箭头对象并隐藏
        CreateArrowObject();
    }
    
    // 创建箭头对象 - 只在游戏开始时调用一次
    private void CreateArrowObject()
    {
        // 检查是否有预制体
        if (arrowPrefab == null)
        {
            Debug.LogWarning("箭头预制体未设置!");
            return;
        }
        
        // 实例化箭头
        arrowObject = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
        
        // 将箭头设为该脚本的子对象，便于管理
        arrowObject.transform.SetParent(transform);
        
        // 初始时隐藏箭头
        arrowObject.SetActive(false);
        
        // 获取SpriteRenderer组件
        arrowRenderer = arrowObject.GetComponentInChildren<SpriteRenderer>();
    }
    
    private void Update()
    {
        if (isShooting)
        {
            UpdateRopeShooting();
        }
        else if (isHooked)
        {
            UpdateRopeHooked();
        }
    }

    
   private void FixedUpdate()
{
    if (isHooked && anchors.Count > 0)
    {
        // 只有当速度超过阈值时才进行预测性检测
        if (playerRigidbody.velocity.magnitude > 5f)
        {
            Vector2 currentPos = playerController.transform.position;
            Vector2 predictedPos = currentPos + (Vector2)playerRigidbody.velocity * Time.fixedDeltaTime;
            PredictiveCollisionCheck(currentPos, predictedPos);
        }
    }
}

// 预测性碰撞检测方法
private void PredictiveCollisionCheck(Vector2 fromPos, Vector2 toPos)
{
    if (anchors.Count == 0) return;
    
    Vector2 movementVector = toPos - fromPos;
    float movementDistance = movementVector.magnitude;
    
    // 根据移动速度动态调整检测步数
    const int maxSteps = 50;
    int dynamicSteps = Mathf.Min(maxSteps, Mathf.Max(swingCollisionSteps, Mathf.CeilToInt(movementDistance * 10)));
    // 速度较低时减少检测步数
    if (playerRigidbody != null && playerRigidbody.velocity.sqrMagnitude < 100f) // 使用sqrMagnitude优化
        dynamicSteps = Mathf.Max(5, dynamicSteps / 2);
    
    // 将移动路径分成多个步骤进行检测
    for (int i = 1; i <= dynamicSteps; i++)
    {
        // 计算当前检测点
        float stepProgress = (float)i / dynamicSteps;
        Vector2 checkPoint = fromPos + movementVector * stepProgress;
        
        // 检查从当前检测点到所有锚点的路径是否有障碍物
        CheckPointToAnchors(checkPoint);

        // 添加元素碰撞检测 - 调用PlayerController的预测性碰撞检测
        if (i > 1) // 跳过第一个点，因为它太接近当前位置
        {
            float prevProgress = (float)(i-1) / dynamicSteps;
            Vector2 prevPoint = fromPos + movementVector * prevProgress;
            playerController.CheckPredictiveElementalCollision(prevPoint, checkPoint, hookableLayers);
        }
    }
}

private void CheckPointToAnchors(Vector2 checkPoint)
{
    // 合并可钩住层和仅碰撞层，用于绳索弯折检测
    LayerMask combinedLayers = hookableLayers | collisionOnlyLayers;
    
    // 检查到第一个锚点
    RaycastHit2D hit = Physics2D.Linecast(checkPoint, anchors[0], combinedLayers);
    if (hit && Vector2.Distance(hit.point, anchors[0]) > anchorSafetyCheck)
    {
        // 获取碰撞物体的标签
        string hitTag = hit.collider.tag;
        
        // 处理特殊物体标签效果
        HandleHookTagEffect(hitTag);
        
        // 计算更安全的锚点位置
        Vector2 safeAnchorPoint = hit.point + (hit.normal.normalized * linecastOffset);
        
        // 检查新锚点是否与现有锚点距离足够远
        if (Vector2.Distance(safeAnchorPoint, anchors[0]) > anchorSafetyCheck)
        {
            // 确保从检测点到新锚点的路径是通畅的
            Vector2 dirToAnchor = (safeAnchorPoint - checkPoint).normalized;
            Vector2 offsetStart = checkPoint + dirToAnchor * 0.2f;
            
            // 再次检查从偏移起点到新锚点是否有障碍物
            RaycastHit2D safetyCheck = Physics2D.Linecast(offsetStart, safeAnchorPoint, combinedLayers);
            
            // 如果没有障碍物或障碍物就是目标点，则添加锚点
            if (!safetyCheck || Vector2.Distance(safetyCheck.point, safeAnchorPoint) < 0.1f)
            {
                AddAnchor(safeAnchorPoint);
            }
        }
    }
}

    // 发射绳索
    public void ShootRope(Vector2 direction)
    {
        if (isShooting || isHooked)
            return;
        
        // 检测玩家朝向
        isFacingLeft = direction.x < 0;
        
        shootDirection = direction.normalized;
        isShooting = true;
        shootDistance = 0f;
        // 重置最大长度相关变量
        isAtMaxLength = false;
        maxLengthTimer = 0f;
        
        // 清空锚点列表
        anchors.Clear();
        combinedAnchorLen = 0f;

        // 重置线渲染器的颜色和宽度到原始状态
        if (lineRenderer != null)
        {
            // 恢复原始颜色
            lineRenderer.startColor = originalRopeColor;
            lineRenderer.endColor = originalRopeColor;
            
            // 恢复原始宽度 - 使用Inspector中设置的默认值
            // 如果这些值在运行时会变化，可以考虑在Awake中保存原始宽度
            lineRenderer.startWidth = originalStartWidth;
            lineRenderer.endWidth = originalEndWidth;
        }
        
        // 启用线渲染器
        lineRenderer.enabled = true;
        
        // 重置线渲染器位置
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, playerController.transform.position);
        
        // 显示箭头并设置初始位置
        ShowArrow(playerController.transform.position);

        GameEvents.TriggerRopeShoot();
    }
    
    // 显示箭头
    private void ShowArrow(Vector2 position)
    {
        if (arrowObject == null)
        {
            CreateArrowObject();
            if (arrowObject == null) return;
        }
        
        arrowObject.SetActive(true);
        arrowObject.transform.position = position;
        UpdateArrowFacing();
    }
    
    // 更新箭头位置
    private void UpdateArrowPosition(Vector2 position)
    {
        if (arrowObject == null) return;
        
        arrowObject.transform.position = position;
        UpdateArrowFacing();
    }

    // 更新箭头朝向
    private void UpdateArrowFacing()
    {
        if (arrowRenderer != null)
        {
            arrowRenderer.flipX = isFacingLeft;
        }
    }
    
    // 更新绳索发射状态
    private void UpdateRopeShooting()
    {
        // 隐藏玩家控制器中的箭头（如果有）
        if (playerController.Gun != null)
            playerController.Gun.SetActive(false);
            
        // 如果已经达到最大长度，开始计时
        if (isAtMaxLength)
        {
            maxLengthTimer += Time.deltaTime;
            
            // 如果超过保持时间，释放绳索
            if (maxLengthTimer >= maxLengthHoldTime)
            {
                ReleaseRope();
                return;
            }
            
            // 保持在最大长度
            shootDistance = ropeLength;
        }
        else
        {
            // 增加绳索长度
            shootDistance += Time.deltaTime * ropeShootSpeed;

            // 检查是否达到最大长度
            if (shootDistance >= ropeLength)
            {
                shootDistance = ropeLength;
                isAtMaxLength = true;
                maxLengthTimer = 0f;
            }
        }

        // 计算当前绳索末端位置
        Vector2 endPosition = (Vector2)playerController.transform.position + shootDirection * shootDistance;

        // 更新线渲染器
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, endPosition);

        // 更新箭头位置
        UpdateArrowPosition(endPosition);

        // 检测可钩住层的碰撞
        RaycastHit2D hookHit = Physics2D.Raycast(
            playerController.transform.position,
            shootDirection,
            shootDistance,
            hookableLayers
        );

        // 检测仅碰撞层的碰撞
        RaycastHit2D collisionHit = Physics2D.Raycast(
            playerController.transform.position,
            shootDirection,
            shootDistance,
            collisionOnlyLayers
        );

        // 如果碰到了仅碰撞层，立即结束发射但不钩住
        if (collisionHit.collider != null)
        {
            // 绳索弹回效果
            ReleaseRope();
            // 可以添加弹回的视觉或音效
            GameEvents.TriggerHookFail();
            return;
        }

        // 如果碰到了可钩住层
        if (hookHit.collider != null)
        {
            // 获取碰撞物体的标签
            string hitTag = hookHit.collider.tag;
            // 检查是否为不可钩住的物体
            if (hitTag == "NotHookable")
            {
                // 绳索弹回效果
                ReleaseRope();
                // 可以添加弹回的视觉或音效
                GameEvents.TriggerHookFail();
                return;
            }
            // 计算更安全的锚点位置，沿法线方向偏移
            Vector2 safeAnchorPoint = hookHit.point + (hookHit.normal.normalized * linecastOffset);
            
            // 绳索已钩住物体
            hookPosition = safeAnchorPoint;
            isHooked = true;
            isShooting = false;
            
            // 保存当前钩中的物体标签
            currentHookTag = hitTag;

            // 添加第一个锚点
            AddAnchor(safeAnchorPoint);
            
            // 钩中目标后隐藏箭头
            if (arrowObject != null)
            {
                arrowObject.SetActive(false);
            }
            
            // 通知玩家控制器进入绳索模式
            playerController.EnterRopeMode();
            // 触发绳索钩住事件
            GameEvents.TriggerRopeHooked(hookPosition);
            HandleHookTagEffect(hitTag);
        }
    }
    
// 处理不同标签的钩索效果
private void HandleHookTagEffect(string tag)
{
    // 保存当前钩中的物体标签
    currentHookTag = tag;
    
    // 调用StatusManager中的方法处理效果
    if (statusManager != null) {
        statusManager.HandleHookTagEffect(tag);
    } else {
        Debug.LogError("没有找到statusManager");
    }
}

    // 更新已钩住状态
    private void UpdateRopeHooked()
    {
        if (distanceJoint == null || !distanceJoint.enabled)
        {
            ReleaseRope();
            return;
        }
        
        // 管理绳索弯曲
        RopeJointManager();
        
        // 更新线渲染器
        UpdateLineRenderer();
    }
    
private void RopeJointManager()
{
    // 检查从玩家到最近锚点的线检测
    if (anchors.Count > 0)
    {
        // 获取玩家位置
        Vector2 playerPos = playerController.transform.position;
        
        // 合并可钩住层和仅碰撞层，用于绳索弯折检测
        LayerMask combinedLayers = hookableLayers | collisionOnlyLayers;
        
        // 检查是否需要添加新的锚点 - 使用合并后的Layer
        RaycastHit2D hit = Physics2D.Linecast(playerPos, anchors[0], combinedLayers);
        if (hit)
        {
            // 获取碰撞物体的标签
            string hitTag = hit.collider.tag;
            
            // 处理特殊物体标签效果
            HandleHookTagEffect(hitTag);
            
            // 计算更安全的锚点位置
            Vector2 safeAnchorPoint = hit.point + (hit.normal.normalized * linecastOffset);
            
            // 检查新锚点是否与现有锚点距离足够远，避免重复添加
            if (Vector2.Distance(safeAnchorPoint, anchors[0]) > anchorSafetyCheck)
            {
                // 确保从玩家到新锚点的路径是通畅的
                Vector2 dirToAnchor = (safeAnchorPoint - playerPos).normalized;
                float distToAnchor = Vector2.Distance(playerPos, safeAnchorPoint);
                
                // 从玩家位置向新锚点方向稍微偏移一点，避免自身碰撞检测
                Vector2 offsetStart = playerPos + dirToAnchor * 0.2f;
                
                // 再次检查从偏移起点到新锚点是否有障碍物
                RaycastHit2D safetyCheck = Physics2D.Linecast(offsetStart, safeAnchorPoint, combinedLayers);
                
                // 如果没有障碍物或障碍物就是目标点，则添加锚点
                if (!safetyCheck || Vector2.Distance(safetyCheck.point, safeAnchorPoint) < 0.1f)
                {
                    AddAnchor(safeAnchorPoint);
                }
            }
        }
        
        // 检查是否可以移除锚点
        if (anchors.Count > 1)
        {
            // 计算玩家到第一个锚点的向量
            Vector2 playerToFirstAnchor = (anchors[0] - playerPos).normalized;
            // 计算第一个锚点到第二个锚点的向量
            Vector2 firstToSecondAnchor = (anchors[1] - anchors[0]).normalized;
            
            // 计算两个向量的点积，用于判断夹角
            float dotProduct = Vector2.Dot(playerToFirstAnchor, firstToSecondAnchor);
            
            // 点积接近-1表示两个向量方向几乎相反，即角度接近180度
            // 这表明玩家-锚点1-锚点2几乎在一条直线上，可以移除第一个锚点
            if (dotProduct < -0.75f) // 约等于角度大于143度
            {
                RemoveAnchor();
            }
            else
            {
                // 保留原有的检测逻辑作为备选
                Vector2 ABVector = (anchors[0] - playerPos).normalized;
                Vector2 shortLCStart = anchors[0] - (0.2f * ABVector);
                
                RaycastHit2D returnHitShort = Physics2D.Linecast(shortLCStart, anchors[1], combinedLayers);
                if (!returnHitShort)
                {
                    // 如果没有障碍物，可以移除第一个锚点
                    RemoveAnchor();
                }
            }
        }
    }
}
    
    // 添加锚点
    private void AddAnchor(Vector2 pos)
    {
        // 避免添加太接近的锚点
        if (anchors.Count > 0 && Vector2.Distance(pos, anchors[0]) < anchorSafetyCheck)
            return;
            
        // 插入到列表开头
        anchors.Insert(0, pos);
        
        // 如果有多个锚点，计算锚点间距离
        if (anchors.Count > 1)
        {
            combinedAnchorLen += Vector2.Distance(anchors[0], anchors[1]);
            combinedAnchorLen = Mathf.Round(combinedAnchorLen * 100f) / 100f;
        }
        
        // 更新关节
        SetJoint();
    }
    
    // 移除第一个锚点
    private void RemoveAnchor()
    {
        if (anchors.Count <= 1)
            return;
            
        // 计算要减去的距离
        combinedAnchorLen -= Vector2.Distance(anchors[0], anchors[1]);
        combinedAnchorLen = Mathf.Round(combinedAnchorLen * 100f) / 100f;
        
        // 检查是否正在移除燃烧节点
        bool removingBurningAnchor = (burningAnchorIndex == 0);
        
        // 移除第一个锚点
        anchors.RemoveAt(0);
        
        // 如果移除的是燃烧节点，更新燃烧状态
        if (removingBurningAnchor)
        {
            // 更新燃烧锚点索引
            burningAnchorIndex = -1;
            
            // 如果玩家处于燃烧状态，恢复正常状态
            if (statusManager != null && statusManager.IsInState(GameEvents.PlayerState.Burning))
            {
                // 恢复绳索外观
                if (lineRenderer != null)
                {
                    lineRenderer.startColor = originalRopeColor;
                    lineRenderer.endColor = originalRopeColor;
                    lineRenderer.startWidth = originalStartWidth;
                    lineRenderer.endWidth = originalEndWidth;
                }
                
                // 触发状态变化事件
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Swinging);
            }
        }
        else if (burningAnchorIndex > 0)
        {
            // 如果删除的锚点在燃烧锚点之前，需要更新燃烧锚点索引
            burningAnchorIndex--;
        }
        
        // 更新关节
        SetJoint();
    }
    
    // 设置关节
    private void SetJoint()
    {
        if (distanceJoint == null || anchors.Count == 0)
            return;
            
        // 计算距离
        float dist = Vector2.Distance(playerController.transform.position, anchors[0]);
        
        // 确保总长度不超过最大长度
        float allowedDistance = maxRopeLength - combinedAnchorLen;
        dist = Mathf.Min(dist, allowedDistance);
        
        // 配置关节
        distanceJoint.connectedAnchor = anchors[0];
        distanceJoint.distance = dist;
        distanceJoint.enabled = true;
        
        // 更新当前绳索长度
        currentRopeLength = dist;
    }
    
    // 更新线渲染器
    private void UpdateLineRenderer()
    {
        if (lineRenderer == null)
            return;
            
        // 设置顶点数量：玩家位置 + 所有锚点
        lineRenderer.positionCount = anchors.Count + 1;
        
        // 设置玩家位置为第一个点
        lineRenderer.SetPosition(0, playerController.transform.position);
        
        // 设置所有锚点
        for (int i = 0; i < anchors.Count; i++)
        {
            lineRenderer.SetPosition(i + 1, anchors[i]);
        }
    }
    #region release rope
    public void ReleaseRope()
    {
        isShooting = false;
        isHooked = false;
        
        // 重置线渲染器的颜色和宽度到原始状态
        if (lineRenderer != null)
        {
            // 恢复原始颜色
            lineRenderer.startColor = originalRopeColor;
            lineRenderer.endColor = originalRopeColor;
            
            // 恢复原始宽度 - 确保清除宽度曲线
            lineRenderer.widthCurve = originalWithCurve; // 清除宽度曲线
            lineRenderer.startWidth = originalStartWidth;
            lineRenderer.endWidth = originalEndWidth;
            lineRenderer.colorGradient = originalColorGradient; // 恢复原始颜色渐变
        }
        
        lineRenderer.enabled = false;

        // 清空锚点
        anchors.Clear();
        combinedAnchorLen = 0f;
        
        // 重置线渲染器
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, playerController.transform.position);
        
        // 禁用关节
        if (distanceJoint != null)
        {
            distanceJoint.enabled = false;
        }
        
        // 隐藏箭头
        if (arrowObject != null)
        {
            arrowObject.SetActive(false);
        }
        
        // 重置绳索长度
        currentRopeLength = ropeLength;
        
        // 重置燃烧状态
        burningAnchorIndex = -1;
        currentHookTag = "";
        
        // 通知玩家控制器退出绳索模式
        playerController.ExitRopeMode();
    }

#endregion
    public void AdjustRopeLength(float direction)
    {
        if (!isHooked || distanceJoint == null || !distanceJoint.enabled || anchors.Count == 0)
            return;
        
        // 如果是伸长绳索 (direction > 0)
        if (direction > 0)
        {
            // 计算绳索的实际路径长度
            float actualRopePathLength = 0f;
            Vector2 prevPoint = playerController.transform.position;
            
            // 计算玩家到所有锚点的路径总长度
            for (int i = 0; i < anchors.Count; i++)
            {
                actualRopePathLength += Vector2.Distance(prevPoint, anchors[i]);
                prevPoint = anchors[i];
            }
            
            // 检查如果增加长度后是否会超过最大长度
            float newRopeLength = currentRopeLength + direction * ropeAdjustSpeed * Time.deltaTime;
            
            // 计算总长度 = 新的关节距离 + 锚点之间的距离
            float totalLength = newRopeLength + combinedAnchorLen;
            
            // 如果总长度超过最大长度，则不再增加
            if (totalLength > maxRopeLength)
            {
                return;
            }
        }
        
        // 存储原来的长度
        float previousLength = currentRopeLength;
        // 正常调整绳索长度
        currentRopeLength += direction * ropeAdjustSpeed * Time.deltaTime;
        
        // 限制长度范围
        // 最大允许的关节距离 = 最大绳索长度 - 锚点之间的距离
        float allowedDistance = maxRopeLength - combinedAnchorLen;
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, allowedDistance);
        
        // 更新关节距离
        distanceJoint.distance = currentRopeLength;

        // 如果长度确实发生了变化，触发事件
        if (Mathf.Abs(previousLength - currentRopeLength) > 0.01f)
        {
            GameEvents.TriggerRopeLengthChanged(currentRopeLength);
        }
    }

    
    
    // 摆动
    public void Swing(float direction)
    {
        if (!isHooked)
            return;
                
        // 计算垂直于绳索的方向
        Vector2 ropeDirection = Vector2.zero;
        
        if (anchors.Count > 0)
        {
            ropeDirection = (anchors[0] - (Vector2)playerController.transform.position).normalized;
        }
        else
        {
            return; // 如果没有锚点，不执行摆动
        }
        
        Vector2 perpendicularDirection = new Vector2(-ropeDirection.y, ropeDirection.x);
        
        // 应用力
        playerRigidbody.AddForce(perpendicularDirection * direction * 10f);
    }
    #region Public Methods

    public void SetCollisionOnlyLayers(LayerMask layers)
{
    collisionOnlyLayers = layers;
}
    // 在指定位置切断绳索
public bool CutRope(Vector2 cutPosition, float cutRadius = 0.5f)
{
    // 如果没有钩住或者没有锚点，则无法切断
    if (!isHooked || anchors.Count == 0)
        return false;
    
    // 检查玩家位置和所有锚点之间的线段是否与切割点相交
    Vector2 playerPos = playerController.transform.position;
    Vector2 prevPoint = playerPos;
    
    // 创建一个新的锚点列表，用于存储切割后保留的锚点
    List<Vector2> remainingAnchors = new List<Vector2>();
    bool ropeCut = false;
    
    // 检查玩家到第一个锚点的线段
    if (IsPointNearLineSegment(playerPos, anchors[0], cutPosition, cutRadius))
    {
        // 如果切割点靠近玩家和第一个锚点之间的线段，直接释放绳索
        ReleaseRope();
        return true;
    }
    
    // 检查锚点之间的线段
    for (int i = 0; i < anchors.Count - 1; i++)
    {
        if (IsPointNearLineSegment(anchors[i], anchors[i + 1], cutPosition, cutRadius))
        {
            // 如果切割点靠近当前锚点和下一个锚点之间的线段
            ropeCut = true;
            
            // 保留切割点之前的锚点
            remainingAnchors = new List<Vector2>(anchors.GetRange(0, i + 1));
            break;
        }
    }
    
    // 如果绳索被切断
    if (ropeCut)
    {
        // 更新锚点列表
        anchors = remainingAnchors;
        
        // 重新计算锚点间距离
        RecalculateAnchorLength();
        
        // 更新关节
        SetJoint();
        
        // 更新线渲染器
        UpdateLineRenderer();
        
        // 触发绳索切断事件
        GameEvents.TriggerRopeCut(cutPosition);
        
        // 可以在这里添加切断效果，如粒子效果或声音
        
        return true;
    }
    
    return false;
}

// 检查点是否靠近线段
private bool IsPointNearLineSegment(Vector2 lineStart, Vector2 lineEnd, Vector2 point, float maxDistance)
{
    // 计算点到线段的距离
    Vector2 line = lineEnd - lineStart;
    float lineLength = line.magnitude;
    Vector2 lineDirection = line.normalized;
    Vector2 pointVector = point - lineStart;
    
    // 计算点在线段上的投影长度
    float projection = Vector2.Dot(pointVector, lineDirection);
    
    // 如果投影在线段外，返回false
    if (projection < 0 || projection > lineLength)
        return false;
    
    // 计算点到线段的垂直距离
    float distance = Vector2.Distance(point, lineStart + lineDirection * projection);
    
    // 如果距离小于最大距离，返回true
    return distance <= maxDistance;
}
    // 检查绳索状态
    // 添加这个新方法来单独检查绳索是否处于发射状态
    public bool IsShooting()
    {
        return isShooting;
    }
    public bool IsRopeShootingOrHooked()
    {
        return isShooting || isHooked;
    }
    
    // 获取钩点位置
    public Vector3 GetHookPosition()
    {
        return hookPosition;
    }
    
    // 设置可钩层
    public void SetHookableLayers(LayerMask layers)
    {
        hookableLayers = layers;
    }

    public Vector2 GetCurrentAnchorPosition()
    {
        if (anchors.Count > 0)
            return anchors[0];
        return Vector2.zero;
    }

    public bool HasAnchors()
    {
        return anchors.Count > 0;
    }
    
    // 获取当前钩中的物体标签
    public string GetCurrentHookTag()
    {
        return currentHookTag;
    }


    private void OnEnable() {
        GameEvents.OnRopeReleased += ReleaseRope;
    }
    
    // 清理资源
    private void OnDisable()
    {
        if (arrowObject != null && Application.isPlaying)
        {
            Destroy(arrowObject);
            arrowObject = null;
        }

        GameEvents.OnRopeReleased -= ReleaseRope;
    }


    // 获取燃烧锚点索引
public int GetBurningAnchorIndex()
{
    return burningAnchorIndex;
}

// 获取燃烧传播速度
public float GetBurnPropagationSpeed()
{
    return burnPropagationSpeed;
}

// 获取燃烧断开阈值
public float GetBurnBreakThreshold()
{
    return burnBreakThreshold;
}

// 获取锚点列表
public List<Vector2> GetAnchors()
{
    return new List<Vector2>(anchors);
}

public AnimationCurve getOriginalCurve()
{
    return originalWithCurve;
}

public Gradient getOriginalColorGradiant()
{
    return originalColorGradient;
}
// 创建火焰粒子
public GameObject CreateFireParticle()
{
    if (fireParticleInstance != null)
    {
        Destroy(fireParticleInstance);
    }
    
    if (fireParticlePrefab != null)
    {
        // 在燃烧锚点位置创建火焰粒子
        Vector3 firePosition = Vector3.zero;
        if (anchors.Count > burningAnchorIndex && burningAnchorIndex >= 0)
        {
            firePosition = anchors[burningAnchorIndex];
        }
        else if (anchors.Count > 0)
        {
            firePosition = anchors[0];
        }
        
        fireParticleInstance = Instantiate(fireParticlePrefab, firePosition, Quaternion.identity);
        return fireParticleInstance;
    }
    
    return null;
}

// 在燃烧点断开绳索
public void BreakRopeAtBurningPoint()
{
    if (burningAnchorIndex < 0 || burningAnchorIndex >= anchors.Count)
        return;
    
    // 保留从玩家到燃烧点的部分，移除燃烧点之后的部分
    if (burningAnchorIndex > 0)
    {
        // 移除从燃烧点开始的所有锚点
        int countToRemove = anchors.Count - burningAnchorIndex;
        anchors.RemoveRange(burningAnchorIndex, countToRemove);
        
        // 重新计算锚点间距离
        RecalculateAnchorLength();
        
        // 更新关节
        SetJoint();
    }
    else
    {
        // 如果燃烧点是第一个锚点，直接释放绳索
        ReleaseRope();
    }
}

// 重新计算锚点间总距离
private void RecalculateAnchorLength()
{
    combinedAnchorLen = 0f;
    
    for (int i = 0; i < anchors.Count - 1; i++)
    {
        combinedAnchorLen += Vector2.Distance(anchors[i], anchors[i + 1]);
    }
    
    combinedAnchorLen = Mathf.Round(combinedAnchorLen * 100f) / 100f;
}
#endregion
#region OnGizmos    
    // 在编辑器中可视化锚点和绳索
    private void OnDrawGizmos()
    {
        // 绘制锚点
        Gizmos.color = Color.red;
        foreach (Vector2 anchor in anchors)
        {
            Gizmos.DrawWireSphere(anchor, 0.25f);
        }
        
        // 绘制偏移距离
        Gizmos.color = Color.green;
        if (anchors.Count > 0)
        {
            // 显示第一个锚点的偏移距离
            Gizmos.DrawWireSphere(anchors[0], linecastOffset);
        }

        // 绘制预测路径
        if (Application.isPlaying && isHooked && playerRigidbody != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 currentPos = playerController.transform.position;
            Vector2 predictedPos = currentPos + (Vector2)playerRigidbody.velocity * Time.fixedDeltaTime;
            Gizmos.DrawLine(currentPos, predictedPos);
            
            // 绘制检测点
            Gizmos.color = Color.cyan;
            int steps = Mathf.Max(swingCollisionSteps, Mathf.CeilToInt(Vector2.Distance(currentPos, predictedPos) * 10));
            for (int i = 1; i <= steps; i++)
            {
                float progress = (float)i / steps;
                Vector2 checkPoint = Vector2.Lerp(currentPos, predictedPos, progress);
                Gizmos.DrawWireSphere(checkPoint, 0.05f);
            }
        }
    }
}
#endregion