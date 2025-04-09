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
    
    [Header("弹簧设置")]
    [Range(0, 1)]
    public float dampingRatio = 0.8f;
    [Range(0, 10)]
    public float frequency = 1.0f;
    
    [Header("断裂设置")]
    public CollisionDetectionMode2D breakAction = CollisionDetectionMode2D.Discrete;
    public float breakForce = Mathf.Infinity;
    
    [Header("弯折设置")]
    public bool enableBending = true;
    public LayerMask obstacleLayer;
    public float bendingDetectionRadius = 0.1f;
    public float minNodeDistance = 0.3f; // 节点之间的最小距离
    public int maxNodes = 10; // 最大节点数量
    public float nodeRadius = 0.1f; // 节点碰撞半径
    
    [Header("可视化设置")]
    public LineRenderer ropeRenderer;
    public bool showGizmos = true;
    public Color ropeColor = Color.white;
    public float ropeWidth = 0.05f;
    
    // 内部变量
    private Rigidbody2D rb;
    private List<BendNode> bendNodes = new List<BendNode>();
    private BendNode activeNode; // 当前与玩家交互的节点
    private Vector2 originalConnectedPosition;
    private Vector2 lastFramePosition;
    private bool isStretched = false;
    
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
        
        // 初始化线渲染器
        if (ropeRenderer == null)
        {
            ropeRenderer = gameObject.AddComponent<LineRenderer>();
            ropeRenderer.startWidth = ropeWidth;
            ropeRenderer.endWidth = ropeWidth;
            ropeRenderer.material = new Material(Shader.Find("Sprites/Default"));
            ropeRenderer.startColor = ropeColor;
            ropeRenderer.endColor = ropeColor;
        }
        
        // 如果自动配置距离，则计算初始距离
        if (autoConfigureDistance && connectedBody != null)
        {
            Vector2 worldAnchor = (Vector2)transform.TransformPoint(anchor);
            Vector2 worldConnectedAnchor = (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
            distance = Vector2.Distance(worldAnchor, worldConnectedAnchor);
        }
        
        originalConnectedPosition = connectedBody != null ? 
            (Vector2)connectedBody.transform.TransformPoint(connectedAnchor) : 
            (Vector2)transform.TransformPoint(connectedAnchor);
        
        lastFramePosition = rb.position;
    }
    
    private void FixedUpdate()
    {
        if (connectedBody == null)
            return;
            
        UpdateRopePhysics();
        DetectCollisionsAndCreateNodes();
        CleanupInactiveNodes();
        UpdateRopeRenderer();
    }
    
    private void UpdateRopePhysics()
    {
        Vector2 anchorWorldPos = (Vector2)transform.TransformPoint(anchor);
        
        // 如果没有弯折点，直接与连接点交互
        if (bendNodes.Count == 0)
        {
            Vector2 connectedAnchorWorldPos = (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
            ApplySpringForce(anchorWorldPos, connectedAnchorWorldPos);
            activeNode = null;
        }
        // 否则，与最近的弯折点交互
        else
        {
            // 找到最后一个节点（离玩家最近的节点）
            BendNode lastNode = bendNodes[bendNodes.Count - 1];
            lastNode.isActive = true;
            activeNode = lastNode;
            
            // 应用弹簧力
            ApplySpringForce(anchorWorldPos, lastNode.position);
        }
        
        // 检查是否超过断裂力
        if (breakForce < Mathf.Infinity)
        {
            // 计算当前拉力
            float currentForce = CalculateCurrentForce();
            if (currentForce > breakForce)
            {
                // 根据断裂动作执行相应操作
                if (breakAction == CollisionDetectionMode2D.Discrete)
                {
                    Destroy(this);
                }
                else
                {
                    enabled = false;
                }
            }
        }
    }
    
    private void ApplySpringForce(Vector2 point1, Vector2 point2)
    {
        // 计算当前距离和方向
        Vector2 direction = point1 - point2;
        float currentDistance = direction.magnitude;
        
        if (currentDistance == 0)
            return;
            
        // 计算目标距离（考虑最小距离）
        float targetDistance = Mathf.Max(distance, 0.01f);
        
        // 只有当绳索被拉伸时才应用力
        if (currentDistance > targetDistance)
        {
            isStretched = true;
            
            // 计算弹簧力
            float springForce = CalculateSpringForce(currentDistance, targetDistance);
            Vector2 force = direction.normalized * springForce;
            
            // 应用力
            rb.AddForceAtPosition(-force, point1);
        }
        else
        {
            isStretched = false;
        }
    }
    
    private float CalculateSpringForce(float currentDistance, float targetDistance)
    {
        // 使用弹簧公式: F = -k * x - c * v
        // k = 弹簧常数，基于频率
        // c = 阻尼系数，基于阻尼比
        
        // 计算弹簧常数 k (基于频率)
        float k = rb.mass * (2 * Mathf.PI * frequency) * (2 * Mathf.PI * frequency);
        
        // 计算阻尼系数 c
        float c = 2 * dampingRatio * Mathf.Sqrt(rb.mass * k);
        
        // 计算位移 x
        float x = currentDistance - targetDistance;
        
        // 计算速度分量 v (在弹簧方向上的速度)
        Vector2 direction = ((Vector2)transform.TransformPoint(anchor) - 
            (activeNode != null ? activeNode.position : (Vector2)connectedBody.transform.TransformPoint(connectedAnchor))).normalized;
        float v = Vector2.Dot(rb.velocity, direction);
        
        // 计算弹簧力
        return k * x + c * v;
    }
    
    private float CalculateCurrentForce()
    {
        if (!isStretched)
            return 0;
            
        Vector2 anchorWorldPos = (Vector2)transform.TransformPoint(anchor);
        Vector2 connectedPos = activeNode != null ? activeNode.position : 
            (Vector2)connectedBody.transform.TransformPoint(connectedAnchor);
            
        float currentDistance = Vector2.Distance(anchorWorldPos, connectedPos);
        float targetDistance = Mathf.Max(distance, 0.01f);
        
        if (currentDistance <= targetDistance)
            return 0;
            
        // 简化的力计算
        float k = rb.mass * (2 * Mathf.PI * frequency) * (2 * Mathf.PI * frequency);
        return k * (currentDistance - targetDistance);
    }
    
    private void DetectCollisionsAndCreateNodes()
    {
        if (!enableBending)
            return;
            
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
            
            // 使用射线检测碰撞
            RaycastHit2D hit = Physics2D.Raycast(start, direction, segmentLength, obstacleLayer);
            if (hit.collider != null)
            {
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
                
                // 创建新节点
                if (!nodeExists && bendNodes.Count < maxNodes)
                {
                    // 确定插入位置
                    int insertIndex = i;
                    if (i >= bendNodes.Count)
                        insertIndex = bendNodes.Count;
                        
                    BendNode newNode = new BendNode(hit.point, hit.normal);
                    bendNodes.Insert(insertIndex, newNode);
                    
                    // 重新计算路径
                    ropePath = GetRopePath();
                    i--; // 重新检查这段
                }
            }
        }
    }
    
    private void CleanupInactiveNodes()
    {
        if (bendNodes.Count == 0)
            return;
            
        // 获取当前绳索路径
        List<Vector2> ropePath = GetRopePath();
        
        // 检查每个节点是否仍然需要
        for (int i = bendNodes.Count - 1; i >= 0; i--)
        {
            BendNode node = bendNodes[i];
            
            // 确定节点前后的点
            Vector2 prevPoint = i == 0 ? 
                (Vector2)connectedBody.transform.TransformPoint(connectedAnchor) : 
                bendNodes[i - 1].position;
                
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
    
    private List<Vector2> GetRopePath()
    {
        List<Vector2> path = new List<Vector2>();
        
        // 添加起点（连接体的锚点）
        if (connectedBody != null)
        {
            path.Add((Vector2)connectedBody.transform.TransformPoint(connectedAnchor));
        }
        else
        {
            path.Add(originalConnectedPosition);
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
    
    private void UpdateRopeRenderer()
    {
        if (ropeRenderer == null)
            return;
            
        List<Vector2> path = GetRopePath();
        
        // 更新线渲染器
        ropeRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            ropeRenderer.SetPosition(i, path[i]);
        }
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
            }
            
            // 绘制绳索路径
            Gizmos.color = ropeColor;
            List<Vector2> path = GetRopePath();
            for (int i = 0; i < path.Count - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
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
