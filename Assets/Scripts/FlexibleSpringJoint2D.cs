using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Physics 2D/Flexible Spring Joint 2D")]
public class FlexibleSpringJoint2D : MonoBehaviour
{
    [Header("关节设置")]
    public Rigidbody2D connectedBody;
    public bool autoConfigureDistance = true;
    public float distance = 1.0f;
    public bool maxDistanceOnly = false;
    public Vector2 anchor = Vector2.zero;
    public Vector2 connectedAnchor = Vector2.zero;
    
    [Header("断裂设置")]
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;
    public enum BreakAction { Destroy, Disable }
    public BreakAction breakAction = BreakAction.Disable;
    
    [Header("弯折设置")]
    public bool enableBending = true;
    public LayerMask obstacleLayer;
    public float bendingDetectionRadius = 0.1f;
    public float minNodeDistance = 0.1f; // 节点之间的最小距离
    public int maxNodes = 10; // 最大节点数量
    public float nodeRadius = 0.1f; // 节点碰撞半径
    
    [Header("方向判断设置")]
    [Range(-1, 1)]
    public float directionDotThreshold = 0.996f; //方向点积阈值，小于此值表示方向已经相反
    public float minNodeCreationInterval = 0.1f; // 最小创建间隔，防止短时间内创建过多节点

    [Header("节点存活保护")]
    public float minNodeLifetime = 0.5f; // 节点最小存活时间，单位秒
    
    [Header("可视化设置")]
    public bool showGizmos = true;
    public Color ropeColor = Color.white;
    public bool showDirectionVectors = true; // 是否显示方向向量
    
    // 内部变量
    private Rigidbody2D rb;
    private List<BendNode> bendNodes = new List<BendNode>();
    private List<Vector2> previousRopePath = new List<Vector2>(); // 存储上一帧的绳结位置
    private GameObject bendNodeProxy;
    private Rigidbody2D bendNodeRigidbody;
    public float playerNoBendMultiplier = 1.5f; // 玩家周围不产生弯折的区域是玩家半径的倍数
    private float lastNodeCreationTime = 0f; // 上次创建节点的时间

    // 新增：存储原始路径相关变量
    private List<RopeBendPath> originalRopePaths = new List<RopeBendPath>();
    private float pathRecordInterval = 0.015f; // 记录路径的间隔时间
    private float lastPathRecordTime = 0f;


    // 新增：原始路径结构
    private class RopeBendPath
    {
        public List<Vector2> pathPoints;
        public float recordTime;
        public bool isActive;
        public Vector2 obstaclePosition; // 障碍物位置，用于标识这段路径
        public float obstacleRadius;     // 障碍物影响半径

        public RopeBendPath(List<Vector2> points, Vector2 obstaclePos, float radius)
        {
            pathPoints = new List<Vector2>(points);
            recordTime = Time.time;
            isActive = true;
            obstaclePosition = obstaclePos;
            obstacleRadius = radius;
        }
    }

    private struct RecentlyRemovedNode
    {
        public Vector2 position;
        public float removalTime;
        public float radius; // 影响范围

        public RecentlyRemovedNode(Vector2 pos, float time, float rad)
        {
            position = pos;
            removalTime = time;
            radius = rad;
        }
    }

    // 添加 DistanceJoint2D 组件
    private DistanceJoint2D distanceJoint;
    private Vector2 originalConnectedAnchor;
    private Rigidbody2D originalConnectedBody;
    private Vector2 previousPlayerPosition;
    
    // 节点类
    [System.Serializable]
    public class BendNode
    {
        public Vector2 position;
        public Vector2 normal; // 碰撞法线
        public bool isActive; // 是否是当前活动节点
        public float creationTime; // 创建时间，用于平滑过渡
        
        // 新增：记录创建时的方向信息
        public Vector2 directionToPrevNode; // 指向前一个节点的方向
        public Vector2 directionToPlayer; // 创建时指向玩家的方向
        public Vector2 playerMovementDirection; // 创建时玩家的移动方向
        
        // 新增：关联的原始路径索引
        public int originalPathIndex = -1;
        
        public BendNode(Vector2 pos, Vector2 norm)
        {
            position = pos;
            normal = norm;
            isActive = false;
            creationTime = Time.time;
            directionToPrevNode = Vector2.zero;
            directionToPlayer = Vector2.zero;
            playerMovementDirection = Vector2.zero;
        }
        
        public BendNode(Vector2 pos, Vector2 norm, Vector2 dirToPrev, Vector2 dirToPlayer, Vector2 playerMoveDir)
        {
            position = pos;
            normal = norm;
            isActive = false;
            creationTime = Time.time;
            directionToPrevNode = dirToPrev.normalized;
            directionToPlayer = dirToPlayer.normalized;
            playerMovementDirection = playerMoveDir.normalized;
        }
    }
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // 初始化 DistanceJoint2D
        distanceJoint = GetComponent<DistanceJoint2D>();
        if (distanceJoint == null)
            distanceJoint = gameObject.AddComponent<DistanceJoint2D>();
        
        // 配置 DistanceJoint2D 但初始时禁用
        ConfigureDistanceJoint();
        distanceJoint.enabled = false; // 确保初始时关节是禁用的

        // 创建弯折点代理对象
        CreateBendNodeProxy();
        
        // 初始化玩家位置
        previousPlayerPosition = transform.position;
    }

    private void CreateBendNodeProxy()
    {
        bendNodeProxy = new GameObject("BendNodeProxy");
        bendNodeProxy.transform.SetParent(transform.parent); // 设置为与玩家相同的父对象
        
        // 添加刚体组件
        bendNodeRigidbody = bendNodeProxy.AddComponent<Rigidbody2D>();
        bendNodeRigidbody.bodyType = RigidbodyType2D.Kinematic; // 设置为运动学刚体，不受力的影响
        bendNodeRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bendNodeRigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        
        // 初始时禁用代理对象
        bendNodeProxy.SetActive(false);
    }
    
    private void ConfigureDistanceJoint()
    {
        if (distanceJoint == null)
            return;
            
        distanceJoint.autoConfigureDistance = autoConfigureDistance;
        distanceJoint.distance = distance;
        distanceJoint.maxDistanceOnly = maxDistanceOnly;
        distanceJoint.anchor = anchor;
        distanceJoint.connectedAnchor = connectedAnchor;
        distanceJoint.connectedBody = connectedBody;
        
        // 存储原始连接信息
        originalConnectedAnchor = connectedAnchor;
        originalConnectedBody = connectedBody;
        
        // 应用断裂参数
        distanceJoint.breakForce = breakForce;
        distanceJoint.breakTorque = breakTorque;
        
        // 允许连接的物体之间碰撞
        distanceJoint.enableCollision = true;
    }
    
    private void Start()
    {
        // 确保在所有组件初始化后再次配置关节，但保持禁用状态
        ConfigureDistanceJoint();
        distanceJoint.enabled = false; // 再次确保初始时关节是禁用的
    }
    
private void Update()
{
    // 只有在关节启用时才进行处理
    if (!distanceJoint.enabled)
        return;
            
    // 处理弯折检测和可视化
    if (enableBending)
    {
        // 新增：定期记录原始路径，只在没有弯折点时
        if (bendNodes.Count == 0 && Time.time - lastPathRecordTime > pathRecordInterval)
        {
            RecordOriginalRopePath();
            lastPathRecordTime = Time.time;
        }
        
        // 清理不再需要的原始路径
        CleanupOriginalPaths();
        
        // 处理弯折节点
        CleanupInactiveNodes();
        DetectCollisionsAndCreateNodes();
        
        // 更新弯折点的物理交互
        UpdateBendNodePhysics();
    }
}
    
    public void FixedUpdate()
    {
        if (enableBending)
        {
            // 记录当前玩家位置，用于计算移动方向
            Vector2 currentPlayerPosition = transform.position;
            // 更新上一帧的玩家位置
            previousPlayerPosition = currentPlayerPosition;
        }
    }

// 新增：记录原始绳索路径，只在没有弯折点时记录
// 还原：记录原始绳索路径，只在没有弯折点时记录
private void RecordOriginalRopePath()
{
    // 只有当没有弯折点时才记录原始路径
    if (bendNodes.Count > 0)
        return;
        
    // 获取当前绳索路径（不包含弯折点）
    List<Vector2> currentPath = new List<Vector2>();
    
    // 添加起点（钩点）
    if (connectedBody != null)
    {
        currentPath.Add((Vector2)connectedBody.transform.TransformPoint(connectedAnchor));
    }
    else if (distanceJoint.enabled)
    {
        currentPath.Add(connectedAnchor);
    }
    else
    {
        currentPath.Add((Vector2)transform.position);
    }
    
    // 添加终点（玩家）
    currentPath.Add((Vector2)transform.TransformPoint(anchor));
    
    // 检测潜在的障碍物
    Vector2 start = currentPath[0];
    Vector2 end = currentPath[1];
    Vector2 direction = (end - start).normalized;
    float distance = Vector2.Distance(start, end);
    
    // 沿着直线路径进行圆形检测
    int checkPoints = Mathf.Max(3, Mathf.FloorToInt(distance / 0.5f));
    Vector2 obstaclePosition = Vector2.zero;
    float obstacleRadius = 0f;
    
    for (int i = 1; i < checkPoints; i++)
    {
        float t = (float)i / checkPoints;
        Vector2 checkPoint = Vector2.Lerp(start, end, t);
        
        // 检测碰撞
        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPoint, bendingDetectionRadius*2f, obstacleLayer);
        if (colliders.Length > 0)
        {
            // 找到最近的碰撞点
            Vector2 closestPoint = colliders[0].ClosestPoint(checkPoint);
            obstacleRadius = bendingDetectionRadius*3f; // 使用比检测半径更大的值作为障碍物影响范围
            obstaclePosition = closestPoint;
            
            // 检查是否已经有相同位置的路径记录
            bool pathExists = false;
            foreach (var path in originalRopePaths)
            {
                if (Vector2.Distance(path.obstaclePosition, closestPoint) < obstacleRadius * 0.5f)
                {
                    pathExists = true;
                    // 无需更新时间戳，原始路径不依赖于时间
                    break;
                }
            }
            
            // 如果没有相同位置的路径记录，添加新记录
            if (!pathExists)
            {
                RopeBendPath newPath = new RopeBendPath(currentPath, closestPoint, obstacleRadius);
                originalRopePaths.Add(newPath);
                
                #if UNITY_EDITOR
                Debug.LogFormat("记录新的原始路径，障碍物位置: {0}, 路径点数: {1}", closestPoint, currentPath.Count);
                #endif
            }
            
            // 找到一个潜在障碍物就足够了
            break;
        }
    }
}

// 修改：清理原始路径，只在被还原使用后清除
private void CleanupOriginalPaths()
{
    // 标记所有已被使用的路径
    bool[] pathUsed = new bool[originalRopePaths.Count];
    
    // 检查每个节点是否关联了原始路径
    foreach (var node in bendNodes)
    {
        if (node.originalPathIndex >= 0 && node.originalPathIndex < originalRopePaths.Count)
        {
            pathUsed[node.originalPathIndex] = true;
        }
    }
    
    // 删除所有已被使用且不再有节点引用的路径
    for (int i = originalRopePaths.Count - 1; i >= 0; i--)
    {
        bool hasReference = false;
        foreach (var node in bendNodes)
        {
            if (node.originalPathIndex == i)
            {
                hasReference = true;
                break;
            }
        }
        
        // 如果没有节点引用这个路径，删除它
        if (!hasReference && pathUsed[i])
        {
            #if UNITY_EDITOR
            Debug.LogFormat("原始路径 {0} 已被使用且无节点引用，清除", i);
            #endif
            
            // 更新所有节点的索引
            for (int j = 0; j < bendNodes.Count; j++)
            {
                if (bendNodes[j].originalPathIndex > i)
                {
                    bendNodes[j].originalPathIndex--;
                }
            }
            
            originalRopePaths.RemoveAt(i);
        }
    }
}

    private void DetectCollisionsAndCreateNodes()
{
    // 检查是否可以创建新节点（时间间隔控制）
    if (Time.time - lastNodeCreationTime < minNodeCreationInterval)
    {
        return; // 如果间隔太短，跳过本次创建
    }
    
    // 获取当前绳索路径
    List<Vector2> ropePath = GetRopePath();
    
    // 获取玩家位置和无弯折区域半径
    Vector2 playerPos = transform.TransformPoint(anchor);
    float noBendRadius = GetPlayerRadius() * playerNoBendMultiplier;
    
    // 获取起点位置（钩点）
    Vector2 hookPos = ropePath[0];
    float hookNoBendRadius = 0.25f; // 钩点周围范围内不创建弯折点
    
    // 检查每段绳索是否与障碍物碰撞
    for (int i = 0; i < ropePath.Count - 1; i++)
    {
        Vector2 start = ropePath[i];
        Vector2 end = ropePath[i + 1];
        Vector2 direction = end - start;
        float segmentLength = direction.magnitude;
        
        if (segmentLength < 0.01f)
            continue;
            
        direction /= segmentLength;
        
        // 使用更精细的步长来提高检测精度，确保步长不大于minNodeDistance
        float stepSize = Mathf.Min(0.1f, minNodeDistance);
        int steps = Mathf.Max(1, Mathf.FloorToInt(segmentLength / stepSize));
        stepSize = segmentLength / steps;
        
        for (int step = 0; step < steps; step++)
        {
            Vector2 rayStart = start + direction * (step * stepSize);
            float rayLength = stepSize;
            
            // 最后一步可能需要调整长度
            if (step == steps - 1)
                rayLength = Vector2.Distance(rayStart, end);
                
            RaycastHit2D hit = Physics2D.Raycast(rayStart, direction, rayLength, obstacleLayer);
            if (hit.collider != null)
            {
                // 检查碰撞点是否在玩家无弯折区域内
                if (Vector2.Distance(hit.point, playerPos) <= noBendRadius)
                    continue; // 跳过这个碰撞点
                
                // 检查碰撞点是否在钩点无弯折区域内
                if (Vector2.Distance(hit.point, hookPos) <= hookNoBendRadius)
                    continue; // 跳过这个碰撞点
                
                // 检查是否已经有接近的节点
                bool nodeExists = false;
                foreach (var node in bendNodes)
                {
                    if (Vector2.Distance(node.position, hit.point) < minNodeDistance)
                    {
                        nodeExists = true;
                        break;
                    }
                }
                
                if (nodeExists)
                    continue; // 如果已有接近的节点，跳过创建
                
                // 节点数量检查
                if (bendNodes.Count >= maxNodes)
                    continue; // 如果节点数量已达上限，跳过创建
                
                // 计算插入位置 - 确保节点按照从钩点到玩家的顺序排列
                int insertIndex = 0;
                for (int j = 0; j < bendNodes.Count; j++)
                {
                    // 计算从钩点到当前节点的距离
                    float distToHook = Vector2.Distance(ropePath[0], bendNodes[j].position);
                    float newDistToHook = Vector2.Distance(ropePath[0], hit.point);
                    
                    if (newDistToHook < distToHook)
                    {
                        insertIndex = j;
                        break;
                    }
                    
                    insertIndex = j + 1;
                }
                
                // 计算方向向量
                Vector2 dirToPrev = (insertIndex > 0) ?
                    (hit.point - bendNodes[insertIndex-1].position).normalized :
                    (hit.point - ropePath[0]).normalized;
                
                Vector2 dirToPlayer = (playerPos - hit.point).normalized;
                Vector2 playerMoveDir = (Vector2)transform.position - previousPlayerPosition;

                // 如果移动方向太小，使用从节点到玩家的方向作为默认方向
                if (playerMoveDir.sqrMagnitude < 0.001f)
                    playerMoveDir = dirToPlayer;
                else
                    playerMoveDir = playerMoveDir.normalized;
                
                // 创建新节点
                BendNode newNode = new BendNode(hit.point, hit.normal, dirToPrev, dirToPlayer, playerMoveDir);                
                // 插入新节点
                bendNodes.Insert(insertIndex, newNode);
                
                // 更新最后创建节点的时间
                lastNodeCreationTime = Time.time;
                
                // 重新计算路径
                ropePath = GetRopePath();
                i--; // 重新检查这段
                break;
            }
        }
    }
    
    // 保存当前路径作为下一帧的前一帧路径
    previousRopePath = new List<Vector2>(ropePath);
    
    // 确保所有节点之间的距离不小于minNodeDistance
    OptimizeNodeSpacing();
}

    private float GetPlayerRadius()
    {
        // 尝试获取玩家碰撞体半径
        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            // 根据碰撞体类型获取半径
            if (playerCollider is CircleCollider2D)
            {
                return ((CircleCollider2D)playerCollider).radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
            else if (playerCollider is BoxCollider2D)
            {
                BoxCollider2D boxCollider = (BoxCollider2D)playerCollider;
                // 使用盒体的半宽作为半径的近似值
                return Mathf.Max(boxCollider.size.x, boxCollider.size.y) * 0.5f * 
                       Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
            else if (playerCollider is CapsuleCollider2D)
            {
                CapsuleCollider2D capsuleCollider = (CapsuleCollider2D)playerCollider;
                // 使用胶囊体的半径
                return capsuleCollider.size.x * 0.5f * Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
        }
        
        // 如果没有碰撞体或不是支持的类型，返回默认值
        return 0.5f; // 默认半径为0.5
    }

    // 优化节点间距，确保所有节点之间的距离不小于minNodeDistance
    private void OptimizeNodeSpacing()
    {
        if (bendNodes.Count < 2)
            return;
            
        // 从后向前遍历，避免因为删除节点导致索引问题
        for (int i = bendNodes.Count - 1; i > 0; i--)
        {
            Vector2 currentPos = bendNodes[i].position;
            Vector2 prevPos = bendNodes[i-1].position;
            
            float distance = Vector2.Distance(currentPos, prevPos);
            
            // 如果两个节点之间的距离小于最小节点距离，移除其中一个
            if (distance < minNodeDistance)
            {
                // 保留靠近钩点的节点（索引较小的节点），移除当前节点
                bendNodes.RemoveAt(i);
                
                #if UNITY_EDITOR
                Debug.LogFormat("优化节点间距：移除了距离过近的节点（距离: {0}，最小要求: {1})", distance, minNodeDistance);
                #endif
            }
        }
    }
    
private bool ShouldRemoveNodeBasedOnDirection(BendNode node, Vector2 currentPlayerPos)
{
    // 如果方向信息未初始化，返回false
    if (node.playerMovementDirection == Vector2.zero || node.directionToPlayer == Vector2.zero)
        return false;

    // 如果节点创建时间不足最小存活时间，不删除
    if (Time.time - node.creationTime < minNodeLifetime)
        return false;
        
    // 计算当前指向玩家的方向
    Vector2 currentDirToPlayer = (currentPlayerPos - node.position).normalized;
    
    // 计算当前玩家移动方向
    Vector2 currentPlayerMoveDir = ((Vector2)transform.position - previousPlayerPosition).normalized;
    
    // 如果当前几乎没有移动，则不满足条件
    if (currentPlayerMoveDir.sqrMagnitude < 0.001f)
        return false;
    
    // 条件1：检查玩家移动方向是否与节点存储的移动方向相反
    float moveDirDot = Vector2.Dot(node.playerMovementDirection, currentPlayerMoveDir);
    bool moveDirOpposite = moveDirDot < -directionDotThreshold; // 方向相反（点积接近-1）
    
    // 条件2：检查当前指向玩家的方向是否已经"超过"了存储的方向
    // 使用叉积判断方向变化
    Vector3 cross = Vector3.Cross(
        new Vector3(node.directionToPlayer.x, node.directionToPlayer.y, 0),
        new Vector3(currentDirToPlayer.x, currentDirToPlayer.y, 0)
    );
    
    // 计算当前方向与存储方向的角度差
    float angle = Vector2.Angle(node.directionToPlayer, currentDirToPlayer);
    bool angleExceeded = angle > 1f; // 角度差超过1度
    
    // 确定是否是在往回摆动中超过了原始方向
    bool isSwingingBack = moveDirOpposite && angleExceeded;
    
    // 额外检查：确保当前移动方向与当前指向玩家的方向是合适的关系
    float moveToPlayerDot = Vector2.Dot(currentPlayerMoveDir, currentDirToPlayer);
    bool properSwingDirection = moveToPlayerDot < 0.3f; // 移动方向与指向玩家的方向不能太接近平行
    
    // 新增：检查节点方向与到钩点方向的关系
    bool shouldRemoveDueToSimilarDirection = false;
    
    // 获取节点在列表中的索引
    int nodeIndex = bendNodes.IndexOf(node);
    
    // 计算从节点到钩点的方向
    Vector2 dirToHook;
    if (connectedBody != null)
    {
        dirToHook = ((Vector2)connectedBody.transform.TransformPoint(connectedAnchor) - node.position).normalized;
    }
    else if (distanceJoint.enabled)
    {
        dirToHook = (connectedAnchor - node.position).normalized;
    }
    else
    {
        dirToHook = ((Vector2)transform.position - node.position).normalized;
    }
    
    // 检查存储的directionToPrevNode与到钩点方向的相似度
    if (node.directionToPrevNode != Vector2.zero)
    {
        float hookDirDot = Vector2.Dot(node.directionToPrevNode, dirToHook);
        
        // 如果方向非常相似（点积接近1），考虑删除节点
        if (hookDirDot > 0.98f) // 角度差小于约18度
        {
            shouldRemoveDueToSimilarDirection = true;
            
            #if UNITY_EDITOR
            Debug.LogFormat("节点 {0} 的存储方向与到钩点方向几乎相同 (点积: {1:F2})，标记为删除", nodeIndex, hookDirDot);
            #endif
        }
    }
    
    // 检查是否在摆动回来的过程中经过了原始方向
    bool swingingBackThroughOriginal = false;
    
    // 检查当前CD方向是否与原始BC方向接近
    if (node.directionToPlayer != Vector2.zero)
    {
        // 计算当前方向与存储方向的点积
        float currentToPrevDot = Vector2.Dot(currentDirToPlayer, node.directionToPlayer);
        
        // 如果点积接近1，表示方向几乎相同，说明在摆动回来的过程中经过了原始方向
        if (currentToPrevDot > directionDotThreshold && moveDirOpposite)
        {
            swingingBackThroughOriginal = true;
            
            #if UNITY_EDITOR
            Debug.LogFormat("节点 {0} 在摆动回来的过程中经过了原始方向 (点积: {1:F2})", nodeIndex, currentToPrevDot);
            #endif
        }
    }
    
    #if UNITY_EDITOR
    if (moveDirOpposite || angleExceeded || shouldRemoveDueToSimilarDirection || swingingBackThroughOriginal)
    {
        Debug.LogFormat("节点方向判断 - 移动方向相反: {0} (点积: {1:F2}), " +
                  "角度已超过: {2} (角度: {3:F2}°), " +
                  "方向相似需删除: {4}, " +
                  "摆动回来经过原方向: {5}, " +
                  "叉积z: {6:F2}, 移动与指向玩家点积: {7:F2}", 
                  moveDirOpposite, moveDirDot,
                  angleExceeded, angle,
                  shouldRemoveDueToSimilarDirection,
                  swingingBackThroughOriginal,
                  cross.z, moveToPlayerDot);
    }
    #endif
    
    // 返回最终判断结果：
    // 1. 往回摆动中超过了原始方向且摆动方向合适
    // 2. 或者节点方向与到钩点方向相似需要优化
    // 3. 或者在摆动回来的过程中经过了原始方向
    return (isSwingingBack && properSwingDirection) || shouldRemoveDueToSimilarDirection || swingingBackThroughOriginal;
}


    private void CleanupInactiveNodes()
    {
        if (bendNodes.Count == 0)
            return;
                
        // 获取当前绳索路径
        List<Vector2> ropePath = GetRopePath();
        Vector2 playerPos = transform.TransformPoint(anchor);
        Vector2 hookPos;
        
        if (connectedBody != null)
        {
            hookPos = connectedBody.transform.TransformPoint(connectedAnchor);
        }
        else
        {
            hookPos = originalConnectedAnchor;
        }
        
        // 检查是否需要删除节点的标志
        bool shouldRemoveNodes = false;
        List<int> nodesToRemove = new List<int>();
        
        // 检查所有节点，判断是否需要删除
        for (int i = 0; i < bendNodes.Count; i++)
        {
            BendNode node = bendNodes[i];
            
            // 确定节点前后的点
            Vector2 prevPoint;
            if (i == 0) 
            {
                // 如果是第一个节点，前一个点是连接点
                if (connectedBody != null)
                {
                    prevPoint = (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
                }
                else if (distanceJoint.enabled)
                {
                    prevPoint = connectedAnchor;
                }
                else
                {
                    prevPoint = (Vector2)transform.position;
                }
            }
            else
            {
                prevPoint = bendNodes[i - 1].position;
            }
                    
            Vector2 nextPoint = i == bendNodes.Count - 1 ? 
                (Vector2)transform.TransformPoint(anchor) : 
                bendNodes[i + 1].position;
                    
            // 检查节点是否仍在障碍物上
            Collider2D coll = Physics2D.OverlapCircle(node.position, nodeRadius, obstacleLayer);
                
            // 检查直线路径是否会穿过障碍物
            bool directPathBlocked = Physics2D.Linecast(prevPoint, nextPoint, obstacleLayer);
                
            // 基于方向判断是否应该删除节点
            bool directionIndicatesRemoval = ShouldRemoveNodeBasedOnDirection(node, playerPos);
                
            // 如果节点不再需要，标记为删除
            if ((coll == null && !directPathBlocked) || directionIndicatesRemoval && (Time.time - node.creationTime > minNodeLifetime))
            {
                nodesToRemove.Add(i);
                shouldRemoveNodes = true;
                
                #if UNITY_EDITOR
                if (directionIndicatesRemoval)
                    Debug.LogFormat("基于方向判断标记节点 {0} 为删除", i);
                else if (coll == null)
                    Debug.LogFormat("节点不再在障碍物上，标记节点 {0} 为删除", i);
                else if (!directPathBlocked)
                    Debug.LogFormat("直线路径不再被阻挡，标记节点 {0} 为删除", i);
                #endif
            }
        }
        
        // 如果需要删除节点，一次性处理所有需要删除的节点
        if (shouldRemoveNodes)
        {
            // 查找可用的原始路径
            int bestPathIndex = -1;
            
            // 遍历所有节点，找到关联的原始路径
            foreach (var node in bendNodes)
            {
                if (node.originalPathIndex >= 0 && node.originalPathIndex < originalRopePaths.Count)
                {
                    bestPathIndex = node.originalPathIndex;
                    break;
                }
            }
            
            // 从后向前删除节点，避免索引问题
            for (int i = nodesToRemove.Count - 1; i >= 0; i--)
            {
                int nodeIndex = nodesToRemove[i];
                if (nodeIndex < bendNodes.Count) // 安全检查
                {
                    bendNodes.RemoveAt(nodeIndex);
                }
            }
            
            #if UNITY_EDITOR
            Debug.LogFormat("一次性删除了 {0} 个节点", nodesToRemove.Count);
            #endif
            
            // 如果还有节点剩余，更新活动节点
            if (bendNodes.Count > 0)
            {
                // 找到新的最近节点
                BendNode newestNode = FindClosestBendNode();
                if (newestNode != null)
                {
                    // 更新代理对象的位置
                    bendNodeProxy.transform.position = newestNode.position;
                    
                    // 更新关节连接到代理对象
                    distanceJoint.connectedBody = bendNodeRigidbody;
                    distanceJoint.connectedAnchor = Vector2.zero; // 代理对象的中心点
                    
                    // 更新距离为玩家到最新弯折点的距离
                    float newDistance = Vector2.Distance(transform.TransformPoint(anchor), newestNode.position);
                    distanceJoint.distance = newDistance;
                    
                    // 标记为活动节点，其他节点标记为非活动
                    foreach (var node in bendNodes)
                    {
                        node.isActive = (node == newestNode);
                    }
                }
            }
            else
            {
                // 如果没有节点了，使用原始路径还原或重置为原始连接
                if (bestPathIndex >= 0)
                {
                    #if UNITY_EDITOR
                    Debug.LogFormat("使用原始路径 {0} 还原绳索位置", bestPathIndex);
                    #endif
                    
                    // 这里可以添加使用原始路径的逻辑，但由于没有节点，直接重置到原始连接
                    ResetToOriginalConnection();
                    
                    // 标记原始路径已被使用
                    originalRopePaths[bestPathIndex].isActive = false;
                }
                else
                {
                    // 如果没有可用的原始路径，重置为原始连接
                    ResetToOriginalConnection();
                }
            }
            
            return;
        }
        
        // 如果没有找到活动节点，但有节点存在，则设置一个节点为活动节点
        bool hasActiveNode = false;
        foreach (var node in bendNodes)
        {
            if (node.isActive)
            {
                hasActiveNode = true;
                break;
            }
        }
        
        if (!hasActiveNode && bendNodes.Count > 0)
        {
            BendNode newestNode = FindClosestBendNode();
            if (newestNode != null)
            {
                newestNode.isActive = true;
                
                // 更新代理对象的位置
                bendNodeProxy.transform.position = newestNode.position;
                
                // 确保代理对象激活
                if (!bendNodeProxy.activeSelf)
                {
                    bendNodeProxy.SetActive(true);
                }
                
                // 更新关节连接到代理对象
                if (distanceJoint.connectedBody != bendNodeRigidbody)
                {
                    distanceJoint.connectedBody = bendNodeRigidbody;
                    distanceJoint.connectedAnchor = Vector2.zero; // 代理对象的中心点
                    
                    // 更新距离为玩家到最新弯折点的距离
                    float newDistance = Vector2.Distance(transform.TransformPoint(anchor), newestNode.position);
                    distanceJoint.distance = newDistance;
                }
            }
        }
    }


    // 更新弯折点的物理交互
    private void UpdateBendNodePhysics()
    {
        if (bendNodes.Count == 0)
        {
            return;
        }
        
        // 遍历5次，确保所有节点都被正确推出障碍物
        for (int iteration = 0; iteration < 5; iteration++)
        {
            bool anyNodeAdjusted = false;
            
            // 处理所有弯折点，检查是否在障碍物内部，如果是则向外推
            for (int i = 0; i < bendNodes.Count; i++)
            {
                BendNode node = bendNodes[i];
                
                // 检查节点是否在障碍物内部
                Collider2D[] colliders = Physics2D.OverlapCircleAll(node.position, nodeRadius, obstacleLayer);
                foreach (var collider in colliders)
                {
                    // 获取最近的表面点
                    Vector2 surfacePoint = collider.ClosestPoint(node.position);
                    
                    // 如果节点在碰撞体内部，最近的表面点会不同于节点位置
                    if ((surfacePoint - node.position).sqrMagnitude > 0.001f)
                    {
                        // 计算从碰撞体中心到节点的方向（用于向外推）
                        Vector2 pushDirection;
                        
                        // 如果有法线信息，优先使用法线方向
                        if (node.normal != Vector2.zero)
                        {
                            pushDirection = node.normal;
                        }
                        else
                        {
                            // 否则使用从节点到表面点的方向
                            pushDirection = (surfacePoint - node.position).normalized;
                        }
                        
                        // 计算需要推动的距离（至少是节点半径）
                        float pushDistance = nodeRadius + 0.2f; // 添加一点额外距离避免卡在边缘
                        
                        // 更新节点位置，将其推到障碍物外部
                        Vector2 newPosition = surfacePoint + pushDirection * pushDistance;
                        
                        #if UNITY_EDITOR
                        Debug.LogFormat("弯折点在障碍物内部，将其向外推: 从 {0} 到 {1}，全局迭代次数: {2}", 
                            node.position, newPosition, iteration+1);
                        #endif
                        
                        // 更新节点位置
                        node.position = newPosition;
                        
                        // 更新法线方向
                        node.normal = pushDirection;
                        
                        // 如果这是当前活动节点，也更新代理对象的位置
                        if (node.isActive && bendNodeProxy != null)
                        {
                            bendNodeProxy.transform.position = newPosition;
                        }
                        
                        anyNodeAdjusted = true;
                        break; // 一次只处理一个碰撞体
                    }
                }
            }
            
            // 如果这次迭代没有调整任何节点位置，说明所有节点已经不在任何障碍物内部，可以提前退出循环
            if (!anyNodeAdjusted)
            {
                #if UNITY_EDITOR
                if (iteration > 0)
                    Debug.LogFormat("所有节点已调整完毕，共迭代 {0} 次", iteration+1);
                #endif
                break;
            }
        }
        
        // 找到最近的弯折点
        BendNode newestNode = FindClosestBendNode();
        if (newestNode != null)
        {
            // 更新代理对象的位置
            bendNodeProxy.transform.position = newestNode.position;
            
            // 确保代理对象激活
            if (!bendNodeProxy.activeSelf)
            {
                bendNodeProxy.SetActive(true);
            }
            
            // 更新关节连接到代理对象
            if (distanceJoint.connectedBody != bendNodeRigidbody)
            {
                distanceJoint.connectedBody = bendNodeRigidbody;
                distanceJoint.connectedAnchor = Vector2.zero; // 代理对象的中心点
                
                // 更新距离为玩家到最新弯折点的距离
                float newDistance = Vector2.Distance(transform.TransformPoint(anchor), newestNode.position);
                distanceJoint.distance = newDistance;
            }
            
            newestNode.isActive = true;
            
            // 将其他节点标记为非活动
            foreach (var node in bendNodes)
            {
                if (node != newestNode)
                    node.isActive = false;
            }
        }
    }
    
    // 找到离玩家最近的弯折点
    private BendNode FindClosestBendNode()
    {
        if (bendNodes.Count == 0)
            return null;
                
        BendNode closest = null;
        float minDistance = float.MaxValue;
        Vector2 playerPos = transform.TransformPoint(anchor);
        
        foreach (var node in bendNodes)
        {
            float dist = Vector2.Distance(playerPos, node.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = node;
            }
        }
        
        return closest;
    }

    // 重置为原始连接
    private void ResetToOriginalConnection()
    {
        if (bendNodeProxy != null)
            bendNodeProxy.SetActive(false);
                
        if (distanceJoint != null && distanceJoint.enabled)
        {
            distanceJoint.connectedBody = originalConnectedBody;
            distanceJoint.connectedAnchor = originalConnectedAnchor;
            
            // 重新计算距离
            Vector2 anchorWorldPos = transform.TransformPoint(anchor);
            Vector2 connectedWorldPos = originalConnectedBody != null ? 
                originalConnectedBody.transform.TransformPoint(originalConnectedAnchor) : 
                originalConnectedAnchor;
                    
            distanceJoint.distance = Vector2.Distance(anchorWorldPos, connectedWorldPos);
        }
    }
    
    // 获取绳索路径
    public List<Vector2> GetRopePath()
    {
        List<Vector2> path = new List<Vector2>();
        
        // 添加起点（连接体的锚点）
        if (connectedBody != null)
        {
            // 如果有连接体，使用连接体的锚点
            path.Add((Vector2)connectedBody.transform.TransformPoint(connectedAnchor));
        }
        else if (distanceJoint.enabled)
        {
            // 如果关节已启用但没有连接体，使用当前设置的连接点
            path.Add(connectedAnchor);
        }
        else
        {
            // 如果关节未启用，使用玩家当前位置作为起点
            path.Add((Vector2)transform.position);
        }
        
        // 添加所有弯折点
        foreach (var node in bendNodes)
        {
            path.Add(node.position);
        }
        
        // 添加终点（这个对象的锚点）
        path.Add((Vector2)transform.TransformPoint(anchor));
        
        return path;
    }
    
    // 监听关节断裂事件
    private void OnJointBreak2D(Joint2D brokenJoint)
    {
        if (brokenJoint == distanceJoint)
        {
            // 关节已断裂，执行清理操作
            bendNodes.Clear();
            originalRopePaths.Clear();
                    
            // 根据断裂动作执行相应操作
            if (breakAction == BreakAction.Destroy)
            {
                Destroy(this);
            }
            else // BreakAction.Disable
            {
                enabled = false;
            }
        }
    }
    
    // 动态调整距离
    public void SetDistance(float newDistance)
    {
        distance = newDistance;
        if (distanceJoint != null)
        {
            distanceJoint.distance = newDistance;
        }
    }
    
    // 启用/禁用关节
    public void EnableJoint(bool enable)
    {
        if (distanceJoint != null)
        {
            if (enable)
            {
                // 启用关节时，存储原始连接信息
                originalConnectedAnchor = distanceJoint.connectedAnchor;
                originalConnectedBody = distanceJoint.connectedBody;
            }
            else
            {
                // 禁用关节时，清理弯折节点和原始路径
                ClearBendNodes();
                originalRopePaths.Clear();
                
                // 确保代理对象被禁用
                if (bendNodeProxy != null)
                    bendNodeProxy.SetActive(false);
            }
            
            distanceJoint.enabled = enable;
        }
    }
    
    // 重新配置关节
    public void ReconfigureJoint()
    {
        ConfigureDistanceJoint();
    }
    
    // 修改 SetConnectedAnchor 方法，更新原始连接信息
    public void SetConnectedAnchor(Vector2 position)
    {
        // 如果有连接体，将世界坐标转换为连接体的局部坐标
        if (connectedBody != null)
        {
            connectedAnchor = connectedBody.transform.InverseTransformPoint(position);
        }
        else
        {
            // 如果没有连接体，直接使用世界坐标
            connectedAnchor = position;
        }
        
        // 存储原始连接信息
        originalConnectedAnchor = connectedAnchor;
        originalConnectedBody = connectedBody;
        
        // 配置关节
        if (distanceJoint != null)
        {
            // 如果没有弯折点，直接更新关节
            if (bendNodes.Count == 0 || !enableBending)
            {
                distanceJoint.connectedAnchor = connectedAnchor;
                distanceJoint.connectedBody = connectedBody;
            }
            // 否则，保持连接到当前活动的弯折点
        }
    }

    // 修改 ClearBendNodes 方法
    public void ClearBendNodes()
    {
        bendNodes.Clear();
        
        // 重置为原始连接
        ResetToOriginalConnection();
    }
       

    private void OnDestroy()
    {
        if (bendNodeProxy != null)
            Destroy(bendNodeProxy);
    }
    

    
    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;
                
        // 绘制锚点
        Gizmos.color = Color.green;
        if (Application.isPlaying)
        {
            Vector2 anchorWorldPos = (Vector2)transform.TransformPoint(anchor);
            Gizmos.DrawWireSphere(anchorWorldPos, 0.1f);
                
            if (connectedBody != null)
            {
                Vector2 connectedAnchorWorldPos = (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
                Gizmos.DrawWireSphere(connectedAnchorWorldPos, 0.1f);
            }
                
            // 绘制弯折点
            Gizmos.color = Color.yellow;
            foreach (var node in bendNodes)
            {
                if (node.isActive)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.yellow;
                        
                Gizmos.DrawWireSphere(node.position, nodeRadius);
                
                // 绘制法线方向
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(node.position, node.normal * 0.5f);
                
                // 新增：绘制存储的方向向量
                if (showDirectionVectors)
                {
                    // 绘制指向前一个节点的方向 - 使用橙色
                    if (node.directionToPrevNode != Vector2.zero)
                    {
                        Gizmos.color = new Color(1f, 0.5f, 0f); // 橙色
                        Gizmos.DrawRay(node.position, node.directionToPrevNode * 0.4f);
                    }
                    
                    // 绘制创建时指向玩家的方向 - 使用紫色
                    if (node.directionToPlayer != Vector2.zero)
                    {
                        Gizmos.color = new Color(0.8f, 0f, 1f); // 紫色
                        Gizmos.DrawRay(node.position, node.directionToPlayer * 0.4f);
                        
                        // 绘制当前玩家与节点之间的方向 - 使用深蓝色
                        Vector2 playerPos = transform.TransformPoint(anchor);
                        Vector2 currentDirToPlayer = (playerPos - node.position).normalized;
                        Gizmos.color = new Color(0f, 0f, 0.8f); // 深蓝色
                        Gizmos.DrawRay(node.position, currentDirToPlayer * 0.4f);
                        
                        // 条件1：检查指向玩家的方向是否一致
                        float playerDirDot = Vector2.Dot(node.directionToPlayer, currentDirToPlayer);
                        bool playerDirConsistent = playerDirDot > directionDotThreshold;
                        
                        // 绘制创建时的玩家移动方向 - 使用绿色
                        Gizmos.color = Color.green;
                        Gizmos.DrawRay(node.position, node.playerMovementDirection * 0.4f);
                        
                        // 绘制当前玩家移动方向 - 使用红色
                        Vector2 currentPlayerMoveDir = ((Vector2)transform.position - previousPlayerPosition).normalized;
                        if (currentPlayerMoveDir.sqrMagnitude > 0.001f)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawRay(node.position, currentPlayerMoveDir * 0.4f);
                            
                            // 条件2：检查玩家移动方向是否相反
                            float moveDirDot = Vector2.Dot(node.playerMovementDirection, currentPlayerMoveDir);
                            bool moveDirOpposite = moveDirDot < -directionDotThreshold;
                            
                            // 如果两个条件都满足，用特殊颜色标记
                            if (playerDirConsistent && moveDirOpposite)
                            {
                                Gizmos.color = Color.yellow;
                                Gizmos.DrawWireSphere(node.position, nodeRadius * 1.5f);
                            }
                        }
                    }
                }
            }
                
            // 绘制绳索路径
            Gizmos.color = ropeColor;
            List<Vector2> path = GetRopePath();
            for (int i = 0; i < path.Count - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
            
            // 新增：绘制碰撞检测点
            if (distanceJoint.enabled && enableBending)
            {
                // 检查每段绳索是否与障碍物碰撞
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2 start = path[i];
                    Vector2 end = path[i + 1];
                    Vector2 direction = end - start;
                    float segmentLength = direction.magnitude;
                    
                    if (segmentLength < 0.01f)
                        continue;
                        
                    direction /= segmentLength;
                    
                    // 绘制检测线段
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(start, end);
                    
                    // 使用多次射线检测来提高精度
                    int steps = Mathf.Max(1, Mathf.FloorToInt(segmentLength / 0.5f));
                    float stepSize = segmentLength / steps;
                    
                    for (int step = 0; step < steps; step++)
                    {
                        Vector2 rayStart = start + direction * (step * stepSize);
                        float rayLength = stepSize;
                        
                        // 最后一步可能需要调整长度
                        if (step == steps - 1)
                            rayLength = Vector2.Distance(rayStart, end);
                        
                        // 绘制检测点
                        Gizmos.color = Color.blue;
                        Gizmos.DrawWireSphere(rayStart, 0.05f);
                        
                        // 使用射线检测碰撞
                        RaycastHit2D hit = Physics2D.Raycast(rayStart, direction, rayLength, obstacleLayer);
                        if (hit.collider != null)
                        {
                            // 绘制碰撞点
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireSphere(hit.point, 0.1f);
                            
                            // 绘制碰撞法线
                            Gizmos.color = Color.magenta;
                            Gizmos.DrawRay(hit.point, hit.normal * 1f);
                        }
                    }
                }
                
                // 额外绘制圆形检测区域
                if (bendNodes.Count == 0)
                {
                    Vector2 start = path[0]; // 钩点
                    Vector2 end = path[path.Count - 1]; // 玩家
                    
                    // 沿着直线路径进行圆形检测
                    int checkPoints = Mathf.Max(3, Mathf.FloorToInt(Vector2.Distance(start, end) / 0.5f));
                    for (int i = 1; i < checkPoints; i++)
                    {
                        float t = (float)i / checkPoints;
                        Vector2 checkPoint = Vector2.Lerp(start, end, t);
                        
                        // 绘制检测圆
                        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f); // 半透明蓝色
                        Gizmos.DrawWireSphere(checkPoint, bendingDetectionRadius);
                        
                        // 检测碰撞
                        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPoint, bendingDetectionRadius, obstacleLayer);
                        if (colliders.Length > 0)
                        {
                            // 找到最近的碰撞点
                            Vector2 closestPoint = colliders[0].ClosestPoint(checkPoint);
                            
                            // 绘制潜在的碰撞点
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireSphere(closestPoint, 0.1f);
                            
                            // 绘制从检测点到碰撞点的连线
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawLine(checkPoint, closestPoint);
                        }
                    }
                }
            }
        }
        else
        {
            // 编辑器模式下简单显示
            Vector2 anchorWorldPos = (Vector2)transform.TransformPoint(anchor);
            Gizmos.DrawWireSphere(anchorWorldPos, 0.1f);
                
            if (connectedBody != null)
            {
                Vector2 connectedAnchorWorldPos = (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
                Gizmos.DrawWireSphere(connectedAnchorWorldPos, 0.1f);
                Gizmos.DrawLine(anchorWorldPos, connectedAnchorWorldPos);
            }
        }
    }

}