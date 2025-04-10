using System.Collections.Generic;
using UnityEngine;

public class RopeSystem : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject arrowPrefab; // 添加箭头预制体引用
    
    [Header("绳索设置")]
    [SerializeField] private float ropeLength = 50f;
    [SerializeField] private float ropeAdjustSpeed = 2f;
    [SerializeField] private float minRopeLength = 1f;
    [SerializeField] private float maxRopeLength = 100f;
    [SerializeField] private float ropeShootSpeed = 50f; // 发射速度移到这里
    
    [Header("碰撞检测")]
    [SerializeField] private LayerMask hookableLayers; // 可以被钩住的层
    private LayerMask obstacleLayers => hookableLayers; // 障碍物层，用于弯折检测
    [SerializeField] private float ropeFrequency = 1f; // 绳索弹性频率
    [SerializeField] private float ropeDampingRatio = 0.5f; // 绳索阻尼比率
    
    // 内部变量
    private bool isShooting = false;
    private bool isHooked = false;
    private Vector2 hookPosition;
    private Vector2 shootDirection;
    private float shootDistance = 0f;
    private float currentRopeLength;
    private FlexibleSpringJoint2D flexibleJoint; // 替换为自定义弹簧关节
    private Rigidbody2D playerRigidbody;
    private GameObject arrowObject; // 重命名为 arrowObject 表示这是持久的箭头对象
    private SpriteRenderer ArrowRenderer;
    private bool isFacingLeft = false; // 添加一个变量来跟踪玩家朝向
    
    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
            
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
            
        playerRigidbody = playerController.GetComponent<Rigidbody2D>();
        
        // 获取自定义弹簧关节组件
        flexibleJoint = playerController.GetFlexibleJoint();
        if (flexibleJoint == null)
        {
            Debug.LogError("找不到FlexibleSpringJoint2D组件");
            return;
        }
        
        // 初始化线渲染器
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        
        // 如果没有设置可钩层，默认设置为Ground层
        if (hookableLayers.value == 0)
            hookableLayers = LayerMask.GetMask("Ground");
            
        // 在初始化时创建箭头对象并隐藏
        CreateArrowObject();

        // 在创建箭头对象后获取SpriteRenderer组件
        if (arrowObject != null)
        {
            ArrowRenderer = arrowObject.GetComponentInChildren<SpriteRenderer>();
        }
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
    
    // 修改ShootRope方法，添加对玩家朝向的检测
    public void ShootRope(Vector2 direction)
    {
        if (isShooting || isHooked)
            return;
        
        // 检测玩家朝向
        isFacingLeft = direction.x < 0;
        
        shootDirection = direction.normalized;
        isShooting = true;
        shootDistance = 0f;
        
        // 启用线渲染器
        lineRenderer.enabled = true;
        
        // 重置线渲染器位置
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, playerController.transform.position);
        
        // 显示箭头并设置初始位置
        ShowArrow(playerController.transform.position);
    }
    
    // 修改箭头显示方法，考虑玩家朝向
    private void ShowArrow(Vector2 position)
    {
        if (arrowObject == null)
        {
            // 如果箭头对象不存在，尝试创建
            CreateArrowObject();
            if (arrowObject == null) return; // 如果仍然创建失败，直接返回
        }
        
        // 显示箭头
        arrowObject.SetActive(true);
        
        // 设置箭头位置
        arrowObject.transform.position = position;
        
        UpdateArrowFacing();
    }
    
    // 更新箭头位置和朝向 - 也需要考虑玩家朝向
    private void UpdateArrowPosition(Vector2 position)
    {
        if (arrowObject == null) return;
        
        // 更新位置
        arrowObject.transform.position = position;
        if (flexibleJoint != null)
            {
                flexibleJoint.SetConnectedAnchor(position);
            }
        
        UpdateArrowFacing();
    }

    private void UpdateArrowFacing()
    {
        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        if (ArrowRenderer != null)
        {
            ArrowRenderer.flipX = isFacingLeft;
        }
    }
    
    private void UpdateRopeShooting()
    {
        playerController.arrow.SetActive(false);
        // 增加绳索长度，使用预设的发射速度
        shootDistance += Time.deltaTime * ropeShootSpeed;
        
        // 检查是否达到最大长度
        if (shootDistance >= ropeLength)
        {
            ReleaseRope();
            return;
        }
        
        // 计算当前绳索末端位置
        Vector2 endPosition = (Vector2)playerController.transform.position + shootDirection * shootDistance;
        
        // 更新线渲染器
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, endPosition);
        
        // 更新箭头位置
        UpdateArrowPosition(endPosition);
        
        // 检测碰撞 - 使用设置的多层检测
        RaycastHit2D hit = Physics2D.Raycast(
            playerController.transform.position,
            shootDirection,
            shootDistance,
            hookableLayers
        );
        
        if (hit.collider != null)
        {
            // 绳索已钩住物体
            hookPosition = hit.point;
            isHooked = true;
            isShooting = false;
            currentRopeLength = Vector2.Distance(playerController.transform.position, hookPosition);
            
            // 设置弹簧关节 - 使用新的公共方法
            flexibleJoint.SetConnectedAnchor(hookPosition);
            flexibleJoint.SetDistance(currentRopeLength);
            flexibleJoint.dampingRatio = ropeDampingRatio;
            flexibleJoint.frequency = ropeFrequency;
            flexibleJoint.ReconfigureJoint();
            // 注意：不要在这里启用关节，而是在EnterRopeMode中启用
            
            // 钩中目标后隐藏箭头
            if (arrowObject != null)
            {
                arrowObject.SetActive(false);
            }
            
            // 通知玩家控制器进入绳索模式
            playerController.EnterRopeMode();

            #if UNITY_EDITOR
            // 可选：输出被钩住的对象信息 - 使用0GC方式
            if (UnityEngine.Profiling.Profiler.enabled)
            {
                // 使用StringBuilder避免字符串连接产生的GC
                System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
                sb.Append("钩住了: ");
                sb.Append(hit.collider.gameObject.name);
                sb.Append(" 在层: ");
                sb.Append(LayerMask.LayerToName(hit.collider.gameObject.layer));
                
                UnityEngine.Profiling.Profiler.BeginSample(sb.ToString());
                UnityEngine.Profiling.Profiler.EndSample();
            }
            #endif
        }
    }
    
    private void UpdateRopeHooked()
    {
        // 使用FlexibleSpringJoint2D的路径更新线渲染器
        if (flexibleJoint != null && flexibleJoint.enabled)
        {
            List<Vector2> ropePath = flexibleJoint.GetRopePath();
            
            if (ropePath.Count > 0)
            {
                // 更新线渲染器
                lineRenderer.positionCount = ropePath.Count;
                for (int i = 0; i < ropePath.Count; i++)
                {
                    lineRenderer.SetPosition(i, ropePath[i]);
                }
            }
            else
            {
                // 如果没有路径，至少显示从玩家到钩点的直线
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, playerController.transform.position);
                lineRenderer.SetPosition(1, hookPosition);
            }
        }
        else
        {
            // 如果关节不可用，显示简单的直线
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, playerController.transform.position);
            lineRenderer.SetPosition(1, hookPosition);
        }
    }
    
    public void ReleaseRope()
    {
        // 在释放绳索前，确保玩家控制器能够获取到当前的速度和方向
        // 这部分代码已经在PlayerController.ExitRopeMode中处理
        
        isShooting = false;
        isHooked = false;
        lineRenderer.enabled = false;

        // 重置线渲染器的点数和位置，防止下次激活时出现多余的点
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, playerController.transform.position);
        lineRenderer.SetPosition(1, playerController.transform.position);
        
        // 使用FlexibleSpringJoint2D的公共方法禁用关节
        flexibleJoint.EnableJoint(false);
        // 清理弯折节点
        flexibleJoint.ClearBendNodes();
        
        // 确保箭头隐藏
        if (arrowObject != null)
        {
            arrowObject.SetActive(false);
        }
        
        // 通知玩家控制器退出绳索模式
        playerController.ExitRopeMode();
    }
    
    public void AdjustRopeLength(float direction)
    {
        if (!isHooked)
            return;
                
        // 调整绳索长度
        currentRopeLength += direction * ropeAdjustSpeed * Time.deltaTime;
        
        // 限制长度范围
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);
        
        // 使用FlexibleSpringJoint2D的公共方法更新距离
        flexibleJoint.SetDistance(currentRopeLength);
    }
    
    public void Swing(float direction)
    {
        if (!isHooked)
            return;
                
        // 计算垂直于绳索的方向
        Vector2 ropeDirection = (hookPosition - (Vector2)playerController.transform.position).normalized;
        Vector2 perpendicularDirection = new Vector2(-ropeDirection.y, ropeDirection.x);
        
        // 应用力
        playerRigidbody.AddForce(perpendicularDirection * direction * 10f);
    }
    
    public bool IsRopeShootingOrHooked()
    {
        return isShooting || isHooked;
    }
    
    public Vector3 GetHookPosition()
    {
        return hookPosition;
    }
    
    // 可选：添加一个方法来设置可钩层
    public void SetHookableLayers(LayerMask layers)
    {
        hookableLayers = layers;
    }
    
    // 当脚本被禁用或对象被销毁时，确保清理资源
    private void OnDisable()
    {
        // 如果游戏结束或场景切换时，确保清理箭头对象
        if (arrowObject != null && Application.isPlaying)
        {
            Destroy(arrowObject);
            arrowObject = null;
        }
    }
}