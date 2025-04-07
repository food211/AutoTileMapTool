using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    
    [Header("钩索设置")]
    [SerializeField] private Transform aimIndicator;
    [SerializeField] private GameObject arrow; // 添加对箭头的引用
    [SerializeField] private float aimRotationSpeed = 120f;
    [SerializeField] private float ropeShootSpeed = 100f; // 0.2秒内达到最大距离
    [SerializeField] private float maxShootLength = 50f; // 按下时绳索伸长的最大距离
    
    [Header("引用")]
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private float maxSwingSpeed = 8f; // 调整这个值来设置最大速度
    
    // 内部变量
    private Rigidbody2D rb;
    private float aimAngle = 0f;
    private bool isGrounded = false;
    private bool isRopeMode = false;
    private float swingForce = 5f;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (ropeSystem == null)
            ropeSystem = GetComponentInChildren<RopeSystem>();
    }
    
    private void Update()
    {
        // 根据绳索状态控制箭头的可见性
        if (ropeSystem.IsRopeShootingOrHooked())
        {
            arrow.SetActive(false); // 绳索发射或激活时隐藏箭头
        }
        else
        {
            arrow.SetActive(true); // 绳索未发射和未激活时显示箭头
        }

        if (isRopeMode)
        {
            HandleRopeMode();
        }
        else
        {
            HandleNormalMode();
        }
    }

    
    private void HandleNormalMode()
    {
        // 左右移动
        float horizontalInput = Input.GetAxis("Horizontal");
        rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
        
        // 改变玩家朝向
        if (horizontalInput != 0)
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

        // 瞄准控制
        if (Input.GetKey(KeyCode.UpArrow))
            aimAngle += aimRotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            aimAngle -= aimRotationSpeed * Time.deltaTime;
            
        // 限制瞄准角度
        aimAngle = Mathf.Clamp(aimAngle, -80f, 80f);
        aimIndicator.rotation = Quaternion.Euler(0, 0, aimAngle);

        // 根据 aimIndicator 的水平翻转调整旋转角度
        float flipMultiplier = aimIndicator.localScale.x > 0 ? 1 : -1; // 如果翻转，角度需要反向
        aimIndicator.rotation = Quaternion.Euler(0, 0, aimAngle * flipMultiplier);
            
        // 跳跃
        if (Input.GetKeyDown(KeyCode.X) && isGrounded)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        
        // 使用道具
        if (Input.GetKeyDown(KeyCode.Z))
        {
            UseItem();
        }
        
        // 发射钩索
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 direction = aimIndicator.right * flipMultiplier; // 根据水平翻转调整发射方向
            ropeSystem.ShootRope(direction, ropeShootSpeed, maxShootLength);
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
            ropeSystem.AdjustRopeLength(-1f);
        }
        
        // 伸长绳索
        if (Input.GetKey(KeyCode.DownArrow))
        {
            ropeSystem.AdjustRopeLength(1f);
        }
        
        // 使用道具
        if (Input.GetKeyDown(KeyCode.Z))
        {
            UseItem();
        }
        
        // 释放钩索
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ropeSystem.ReleaseRope();
            isRopeMode = false;
        }
        // 获取输入
        float horizontalInput = Input.GetAxis("Horizontal");
        
        // 计算摇摆方向
        Vector2 playerToHook = (Vector2)(ropeSystem.GetHookPosition() - transform.position);
        Vector2 perpendicularDirection = Vector2.Perpendicular(playerToHook.normalized);
        
        // 应用摇摆力
        rb.AddForce(perpendicularDirection * horizontalInput * swingForce, ForceMode2D.Force);
        
        // 限制最大摇摆速度
        LimitSwingSpeed();

        float angle = Vector2.SignedAngle(Vector2.up, playerToHook);
        float angleFactor = Mathf.Cos(angle * Mathf.Deg2Rad);
        // 当角度接近90度或-90度时，力会自然减小
        swingForce *= Mathf.Abs(angleFactor);
    }

    private void LimitSwingSpeed()
    {
        
        // 获取当前速度
        Vector2 currentVelocity = rb.velocity;
        
        // 计算从玩家到钩子的方向
        Vector2 playerToHook = (Vector2)(ropeSystem.GetHookPosition() - transform.position).normalized;
        
        // 计算速度在绳索方向上的分量
        float velocityAlongRope = Vector2.Dot(currentVelocity, playerToHook);
        Vector2 velocityAlongRopeVector = playerToHook * velocityAlongRope;
        
        // 计算垂直于绳索的速度分量（即摇摆速度）
        Vector2 swingVelocity = currentVelocity - velocityAlongRopeVector;
        
        // 如果摇摆速度超过最大值，则限制它
        if (swingVelocity.magnitude > maxSwingSpeed)
        {
            // 限制摇摆速度
            swingVelocity = swingVelocity.normalized * maxSwingSpeed;
            
            // 重新组合速度（保持绳索方向上的速度不变）
            rb.velocity = velocityAlongRopeVector + swingVelocity;
        }
        // 添加轻微阻尼以防止永久摆动
        float dampingFactor = 0.05f;
        rb.AddForce(-swingVelocity * dampingFactor, ForceMode2D.Force);
    }
    
    private void UseItem()
    {
        // 道具使用逻辑
        Debug.Log("使用道具");
    }
    
    // 由RopeSystem调用，进入钩索模式
    public void EnterRopeMode()
    {
        // 进入绳索模式时隐藏箭头
        if (arrow != null)
            arrow.SetActive(false);
        isRopeMode = true;
    }
    
    // 由RopeSystem调用，退出钩索模式
    public void ExitRopeMode()
    {
        // 退出绳索模式时显示箭头
        if (arrow != null)
            arrow.SetActive(true);
        isRopeMode = false;
    }
    
    // 地面检测
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
    
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }

        public float GetMaxShootRopeLength()
    {
        return maxShootLength;
    }
}
