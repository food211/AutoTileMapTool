using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlexibleSpringJoint2D : MonoBehaviour
{
    [Header("连接设置")]
    public Rigidbody2D connectedBody;
    public Vector2 anchor = Vector2.zero;
    public Vector2 connectedAnchor = Vector2.zero;
    public bool autoConfigureDistance = true;
    public float distance = 1.0f;
    public bool maxDistanceOnly = false; // 是否只维持最大距离
    
    [Header("弹簧设置")]
    // 注意：DistanceJoint2D 没有内置的 dampingRatio 和 frequency 参数
    // 这些参数将在自定义逻辑中使用
    [Range(0, 1)]
    public float dampingRatio = 0.8f;
    [Range(0, 10)]
    public float frequency = 1.0f;
    
    public enum BreakAction { Destroy, Disable }
    public BreakAction breakAction = BreakAction.Disable;
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;
    
    [Header("弯折设置")]
    public bool enableBending = true;
    public LayerMask obstacleLayer;
    public float bendingDetectionRadius = 0.1f;
    public float minNodeDistance = 0.1f; // 节点之间的最小距离
    public int maxNodes = 10; // 最大节点数量
    public float nodeRadius = 0.1f; // 节点碰撞半径
    
    [Header("可视化设置")]
    public bool showGizmos = true;
    public Color ropeColor = Color.white;
    
    // 内部变量
    private Rigidbody2D rb;
    private List<BendNode> bendNodes = new List<BendNode>();
    private GameObject bendNodeProxy;
    private Rigidbody2D bendNodeRigidbody;

    // 添加 DistanceJoint2D 组件
    private DistanceJoint2D distanceJoint;
    private Vector2 originalConnectedAnchor;
    private Rigidbody2D originalConnectedBody;
    
    // 节点类
    [System.Serializable]
    public class BendNode
    {
        public Vector2 position;
        public Vector2 normal; // 碰撞法线
        public bool isActive; // 是否是当前活动节点
        public float creationTime; // 创建时间，用于平滑过渡
        
        public BendNode(Vector2 pos, Vector2 norm)
        {
            position = pos;
            normal = norm;
            isActive = false;
            creationTime = Time.time;
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
            CleanupInactiveNodes();
            DetectCollisionsAndCreateNodes();
            // 更新弯折点的物理交互
            UpdateBendNodePhysics();
        }
    }
    
    private void DetectCollisionsAndCreateNodes()
{
    // 获取当前绳索路径
    List<Vector2> ropePath = GetRopePath();
    
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
        float stepSize = Mathf.Min(0.5f, minNodeDistance);
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
                // 检查是否已经有接近的节点，使用精确的minNodeDistance作为判断标准
                bool nodeExists = false;
                foreach (var node in bendNodes)
                {
                    if (Vector2.Distance(node.position, hit.point) < minNodeDistance)
                    {
                        nodeExists = true;
                        break;
                    }
                }
                
                // 创建新节点
                if (!nodeExists && bendNodes.Count < maxNodes)
                {
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
                    
                    BendNode newNode = new BendNode(hit.point, hit.normal);
                    bendNodes.Insert(insertIndex, newNode);
                    
                    // 重新计算路径
                    ropePath = GetRopePath();
                    i--; // 重新检查这段
                    break;
                }
            }
        }
    }
    
    // 添加额外的圆形检测
    if (bendNodes.Count == 0)
    {
        Vector2 start = ropePath[0]; // 钩点
        Vector2 end = ropePath[ropePath.Count - 1]; // 玩家
        
        // 沿着直线路径进行圆形检测，使用minNodeDistance来确定检测点间距
        float totalDistance = Vector2.Distance(start, end);
        int checkPoints = Mathf.Max(3, Mathf.FloorToInt(totalDistance / minNodeDistance));
        
        for (int i = 1; i < checkPoints; i++)
        {
            float t = (float)i / checkPoints;
            Vector2 checkPoint = Vector2.Lerp(start, end, t);
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPoint, bendingDetectionRadius, obstacleLayer);
            foreach (var collider in colliders)
            {
                // 找到最近的碰撞点
                Vector2 closestPoint = collider.ClosestPoint(checkPoint);
                
                // 检查是否已经有接近的节点，使用精确的minNodeDistance作为判断标准
                bool nodeExists = false;
                foreach (var node in bendNodes)
                {
                    if (Vector2.Distance(node.position, closestPoint) < minNodeDistance)
                    {
                        nodeExists = true;
                        break;
                    }
                }
                
                if (!nodeExists && bendNodes.Count < maxNodes)
                {
                    #if UNITY_EDITOR
                    Debug.Log($"通过圆形检测创建弯折节点: {closestPoint}");
                    #endif
                    BendNode newNode = new BendNode(closestPoint, (checkPoint - closestPoint).normalized);
                    bendNodes.Add(newNode);
                    return; // 每次只添加一个节点，避免一次添加太多
                }
            }
        }
    }
    
    // 确保所有节点之间的距离不小于minNodeDistance
    OptimizeNodeSpacing();
}

// 新增方法：优化节点间距，确保所有节点之间的距离不小于minNodeDistance
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
            Debug.Log($"优化节点间距：移除了距离过近的节点（距离: {distance}，最小要求: {minNodeDistance}）");
            #endif
        }
    }
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
    
    // 检查是否可以完全移除所有弯折点 - 从OptimizeBendNodes合并过来
    bool directPathToHook = !Physics2D.Linecast(playerPos, hookPos, obstacleLayer);
    if (directPathToHook && bendNodes.Count > 0)
    {
        #if UNITY_EDITOR
        Debug.Log("优化路径：移除所有弯折点，直接连接到钩点");
        #endif
        bendNodes.Clear();
        ResetToOriginalConnection();
        return;
    }
    
    // 从玩家当前连接的节点开始向前查找 - 从OptimizeBendNodes合并
    BendNode currentNode = FindClosestBendNode();
    if (currentNode != null)
    {
        int currentIndex = bendNodes.IndexOf(currentNode);
        if (currentIndex > 0) // 不是最靠近钩点的节点
        {
            // 尝试跳过当前节点，直接连接到前一个节点
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                BendNode targetNode = bendNodes[i];
                
                // 检查玩家到目标节点之间是否有障碍物
                bool pathClear = !Physics2D.Linecast(playerPos, targetNode.position, obstacleLayer);
                
                if (pathClear)
                {
                    #if UNITY_EDITOR
                    Debug.Log($"优化路径：删除节点 {i+1} 到 {currentIndex}");
                    #endif
                    
                    // 删除中间的节点
                    int removeCount = currentIndex - i;
                    bendNodes.RemoveRange(i + 1, removeCount);
                    
                    // 更新物理连接到新的节点
                    UpdateBendNodePhysics();
                    break;
                }
            }
        }
    }
        
    // 检查每个节点是否仍然需要
    for (int i = bendNodes.Count - 1; i >= 0; i--)
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
                // 如果关节已启用但没有连接体，使用当前设置的连接点
                prevPoint = connectedAnchor;
            }
            else
            {
                // 如果关节未启用，使用玩家当前位置
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
            
        // 如果节点不再需要，移除它
        if (coll == null || !directPathBlocked)
        {
            // 使用平滑过渡，而不是立即移除
            float nodeLifetime = Time.time - node.creationTime;
            if (nodeLifetime > 0.5f) // 给节点一些存在时间，防止抖动
            {
                bendNodes.RemoveAt(i);
            }
        }
    }
}


    // 新增方法：更新弯折点的物理交互
    private void UpdateBendNodePhysics()
{
    if (bendNodes.Count == 0)
    {
        // 如果没有弯折点，恢复原始连接
        ResetToOriginalConnection();
        return;
    }
    
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
                float pushDistance = nodeRadius + 0.05f; // 添加一点额外距离避免卡在边缘
                
                // 更新节点位置，将其推到障碍物外部
                Vector2 newPosition = surfacePoint + pushDirection * pushDistance;
                
                #if UNITY_EDITOR
                Debug.Log($"弯折点在障碍物内部，将其向外推: 从 {node.position} 到 {newPosition}");
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
            }
        }
    }
    
    // 找到离玩家最近的弯折点
    BendNode closestNode = FindClosestBendNode();
    if (closestNode != null)
    {
        // 更新代理对象的位置
        bendNodeProxy.transform.position = closestNode.position;
        
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
            
            // 更新距离为玩家到最近弯折点的距离
            float newDistance = Vector2.Distance(transform.TransformPoint(anchor), closestNode.position);
            distanceJoint.distance = newDistance;
        }
        
        closestNode.isActive = true;
        
        // 将其他节点标记为非活动
        foreach (var node in bendNodes)
        {
            if (node != closestNode)
                node.isActive = false;
        }
    }
}


    
    // 新增方法：找到离玩家最近的弯折点
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

    
    // 新增方法：重置为原始连接
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
    
    // 公共方法，用于获取绳索路径
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
            // 注意：这里不使用缓存的originalConnectedPosition，而是直接使用connectedAnchor
            // 因为connectedAnchor是当前设置的连接点
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
    
    // 公开方法，用于动态调整距离
    public void SetDistance(float newDistance)
    {
        distance = newDistance;
        if (distanceJoint != null)
        {
            distanceJoint.distance = newDistance;
        }
    }
    
    // 修改 EnableJoint 方法，加入对原始连接的存储
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
                // 禁用关节时，清理弯折节点
                ClearBendNodes();
                
                // 确保代理对象被禁用
                if (bendNodeProxy != null)
                    bendNodeProxy.SetActive(false);
            }
            
            distanceJoint.enabled = enable;
        }
    }
    
    // 公开方法，用于重新配置关节
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
                            Gizmos.DrawRay(hit.point, hit.normal * 0.5f);
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