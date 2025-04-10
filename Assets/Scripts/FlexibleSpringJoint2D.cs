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
    public float minNodeDistance = 0.3f; // 节点之间的最小距离
    public int maxNodes = 10; // 最大节点数量
    public float nodeRadius = 0.1f; // 节点碰撞半径
    
    [Header("可视化设置")]
    public bool showGizmos = true;
    public Color ropeColor = Color.white;
    
    // 内部变量
    private Rigidbody2D rb;
    private List<BendNode> bendNodes = new List<BendNode>();
    private BendNode activeNode; // 当前与玩家交互的节点
    
    // 添加 DistanceJoint2D 组件
    private DistanceJoint2D distanceJoint;
    
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
    
    private void FixedUpdate()
    {
        // 只有在关节启用时才进行处理
        if (!distanceJoint.enabled)
            return;
                
        // 处理弯折检测和可视化
        if (enableBending)
        {
            DetectCollisionsAndCreateNodes();
            CleanupInactiveNodes();
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
    
    // 公开方法，用于启用/禁用关节
    public void EnableJoint(bool enable)
    {
        if (distanceJoint != null)
        {
            distanceJoint.enabled = enable;
            
            // 当禁用关节时，重置连接点位置
            if (!enable)
            {
                ClearBendNodes();
            }
        }
    }
    
    // 公开方法，用于重新配置关节
    public void ReconfigureJoint()
    {
        ConfigureDistanceJoint();
    }
    
    // 公开方法，用于设置连接点和配置关节
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
        
        // 配置关节
        if (distanceJoint != null)
        {
            distanceJoint.connectedAnchor = connectedAnchor;
            distanceJoint.connectedBody = connectedBody;
        }
    }

    // 公开方法，用于清理所有弯折节点
    public void ClearBendNodes()
    {
        bendNodes.Clear();
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