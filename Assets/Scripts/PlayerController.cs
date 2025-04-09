using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    
    [Header("钩索设置")]
    [SerializeField] private Transform aimIndicator;
    [SerializeField] public GameObject arrow; // 添加对箭头的引用
    [SerializeField] private float aimRotationSpeed = 120f;
    
    [Header("引用")]
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private float swingForce = 5f;
    [SerializeField] private float maxSwingSpeed = 80f; // 调整这个值来设置最大速度
    
    [Header("物理材质设置")]
    [SerializeField] private PhysicsMaterial2D bouncyBallMaterial; // BouncyBall物理材质
    private PhysicsMaterial2D originalMaterial; // 存储原始物理材质
    
    [Header("地面检测设置")]
    [SerializeField] private LayerMask groundLayers; // 地面层
    [SerializeField] private float maxFallingSpeed = -5f; // Y轴速度上限，可调整
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f); // 地面检测区域的大小
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.05f); // 地面检测区域的偏移量
    [SerializeField] private bool showGroundCheck = true; // 是否在编辑器中显示地面检测区域
    
    // 内部变量
    private Rigidbody2D rb;
    private float aimAngle = 0f;
    private bool isGrounded = false;
    private bool isRopeMode = false;
    private Collider2D playerCollider;
    
    private void Awake()
{
    isRopeMode = false;
    rb = GetComponent<Rigidbody2D>();
    playerCollider = GetComponent<Collider2D>();
    if (ropeSystem == null)
        ropeSystem = GetComponentInChildren<RopeSystem>();
    
    // 保存原始物理材质
    originalMaterial = rb.sharedMaterial;
}
    
    private void Update()
    {
        if (isRopeMode)
        {
            HandleRopeMode();
        }
        else
        {
            HandleNormalMode();
            CheckGrounded();
        }
    }

    // 新的地面检测方法
    private void CheckGrounded()
    {
        // 条件1: 检查Y轴速度是否在允许范围内（maxFallingSpeed到0之间）
        bool isYVelocityInRange = rb.velocity.y >= maxFallingSpeed && rb.velocity.y <= 0;
        
        // 条件2: 检查自定义区域内是否有碰撞
        bool isGroundDetected = CheckGroundCollision();
        
        // 两个条件都满足时，认为角色在地面上
        isGrounded = isYVelocityInRange && isGroundDetected;
    }
    
    // 使用自定义区域检测地面碰撞
    private bool CheckGroundCollision()
    {
        // 获取玩家位置
        Vector2 position = transform.position;
        
        // 计算检测区域的中心点
        Vector2 center = position + groundCheckOffset;
        
        // 使用OverlapBox检测指定区域内的碰撞体
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, groundCheckSize, 0f, groundLayers);
        
        // 在编辑器中可视化检测区域
        #if UNITY_EDITOR
        if (showGroundCheck)
        {
            // 预先计算点位置以避免重复创建Vector3
            Color debugColor = colliders.Length > 0 ? Color.green : Color.red;
            Vector3 bottomLeft = new Vector3(center.x - groundCheckSize.x/2, center.y - groundCheckSize.y/2);
            Vector3 bottomRight = new Vector3(center.x + groundCheckSize.x/2, center.y - groundCheckSize.y/2);
            Vector3 topRight = new Vector3(center.x + groundCheckSize.x/2, center.y + groundCheckSize.y/2);
            Vector3 topLeft = new Vector3(center.x - groundCheckSize.x/2, center.y + groundCheckSize.y/2);
            
            // 使用预先计算的点绘制线条
            Debug.DrawLine(bottomLeft, bottomRight, debugColor);
            Debug.DrawLine(bottomRight, topRight, debugColor);
            Debug.DrawLine(topRight, topLeft, debugColor);
            Debug.DrawLine(topLeft, bottomLeft, debugColor);
        }
        #endif
        
        // 如果检测到任何碰撞体，则认为有地面支撑
        return colliders.Length > 0;
    }
    
    private void OnDrawGizmosSelected()
    {
        // 在Scene视图中可视化地面检测区域
        if (showGroundCheck)
        {
            Vector2 position = transform.position;
            Vector2 center = position + groundCheckOffset;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, groundCheckSize);
        }
    }
    
    private void HandleNormalMode()
{
    // 检查绳索是否正在发射或已钩住
    bool isRopeBusy = ropeSystem.IsRopeShootingOrHooked();
    
    // 左右移动 - 只在地面上时完全控制，在空中时保持惯性
    float horizontalInput = Input.GetAxis("Horizontal");
    
    if (isGrounded)
    {
        // 在地面上时完全控制移动
        rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
    }
    else
    {
        // 在空中时，只施加少量力以微调方向，但保持惯性
        rb.AddForce(new Vector2(horizontalInput * moveSpeed * 0.5f, 0), ForceMode2D.Force);
        
        // 可选：限制最大水平速度
        if (Mathf.Abs(rb.velocity.x) > moveSpeed * 1.5f)
        {
            float clampedXVelocity = Mathf.Clamp(rb.velocity.x, -moveSpeed * 1.5f, moveSpeed * 1.5f);
            rb.velocity = new Vector2(clampedXVelocity, rb.velocity.y);
        }
    }
    
    // 改变玩家朝向 - 只在绳索未发射时
    if (horizontalInput != 0 && !isRopeBusy)
    {
        // 如果向右移动，朝向右边
        if (horizontalInput > 0)
        {
            aimIndicator.transform.localScale = new Vector3(1, 1, 1); // 正常比例，朝右
        }
        // 如果向左移动，朝向左边
        else if (horizontalInput < 0)
        {
            aimIndicator.transform.localScale = new Vector3(-1, 1, 1); // X轴反转，朝左
        }
    }

    // 瞄准控制 - 只在绳索未发射时
    if (!isRopeBusy)
    {
        if (Input.GetKey(KeyCode.UpArrow))
            aimAngle += aimRotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            aimAngle -= aimRotationSpeed * Time.deltaTime;
            
        // 限制瞄准角度
        aimAngle = Mathf.Clamp(aimAngle, -80f, 80f);
        
        // 根据 aimIndicator 的水平翻转调整旋转角度
        float flipMultiplier = aimIndicator.localScale.x > 0 ? 1 : -1; // 如果翻转，角度需要反向
        aimIndicator.rotation = Quaternion.Euler(0, 0, aimAngle * flipMultiplier);
    }
        
    // 跳跃 - 只在绳索未发射时
    if (Input.GetKeyDown(KeyCode.X) && isGrounded && !isRopeBusy)
    {
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }
    
    // 使用道具 - 只在绳索未发射时
    if (Input.GetKeyDown(KeyCode.Z) && !isRopeBusy)
    {
        UseItem();
    }
    
    // 发射钩索 - 只在绳索未发射时
    if (Input.GetKeyDown(KeyCode.Space) && !isRopeBusy)
    {
        Vector2 direction = aimIndicator.right * (aimIndicator.localScale.x > 0 ? 1 : -1);
        ropeSystem.ShootRope(direction);
    }
}


    
    private void HandleRopeMode()
    {
        // 左右摆动
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            ropeSystem.Swing(1 * swingForce);
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            ropeSystem.Swing(-1 * swingForce);
        }
        
        // 收缩绳索
        if (Input.GetKey(KeyCode.UpArrow))
        {
            ropeSystem.AdjustRopeLength(-5f);
        }
        
        // 伸长绳索
        if (Input.GetKey(KeyCode.DownArrow))
        {
            ropeSystem.AdjustRopeLength(5f);
        }
        
        // 释放绳索
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ropeSystem.ReleaseRope();
        }
        
        // 限制最大速度
        LimitMaxVelocity();
    }
    
    // 限制最大速度的方法
    private void LimitMaxVelocity()
    {
        if (rb.velocity.magnitude > maxSwingSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSwingSpeed;
        }
    }
    
    
    // 进入绳索模式
public void EnterRopeMode()
{
    isRopeMode = true;
    arrow.SetActive(false);
    
    // 应用弹性物理材质
    if (bouncyBallMaterial != null)
    {
        rb.sharedMaterial = bouncyBallMaterial;
    }
}
    
    // 退出绳索模式
public void ExitRopeMode()
{
    isRopeMode = false;
    arrow.SetActive(true);
    
    // 恢复原始物理材质
    rb.sharedMaterial = originalMaterial;
}

    
    private void UseItem()
    {
        #if UNITY_EDITOR
        // 道具使用逻辑 - 使用0GC方式
        if (UnityEngine.Profiling.Profiler.enabled)
        {
            UnityEngine.Profiling.Profiler.BeginSample("使用道具");
            UnityEngine.Profiling.Profiler.EndSample();
        }
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
}
