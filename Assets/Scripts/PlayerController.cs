using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    
    [Header("钩索设置")]
    [SerializeField] private Transform aimIndicator;
    [SerializeField] private float aimRotationSpeed = 120f;
    [SerializeField] private float maxRopeLength = 20f;
    [SerializeField] private float ropeShootSpeed = 100f; // 0.2秒内达到最大距离
    
    [Header("引用")]
    [SerializeField] private RopeSystem ropeSystem;
    
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
        
        // 瞄准控制
        if (Input.GetKey(KeyCode.UpArrow))
            aimAngle += aimRotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            aimAngle -= aimRotationSpeed * Time.deltaTime;
            
        // 限制瞄准角度
        aimAngle = Mathf.Clamp(aimAngle, -80f, 80f);
        aimIndicator.rotation = Quaternion.Euler(0, 0, aimAngle);
        
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
            Vector2 direction = aimIndicator.right;
            ropeSystem.ShootRope(direction, ropeShootSpeed, maxRopeLength);
        }
    }
    
    private void HandleRopeMode()
    {
        // 左右摆动
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            ropeSystem.Swing(-1 * swingForce);
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            ropeSystem.Swing(1 * swingForce);
        }
        
        // 收缩绳索
        if (Input.GetKey(KeyCode.UpArrow))
        {
            ropeSystem.AdjustRopeLength(-0.1f);
        }
        
        // 伸长绳索
        if (Input.GetKey(KeyCode.DownArrow))
        {
            ropeSystem.AdjustRopeLength(0.1f);
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
    }
    
    private void UseItem()
    {
        // 道具使用逻辑
        Debug.Log("使用道具");
    }
    
    // 由RopeSystem调用，进入钩索模式
    public void EnterRopeMode()
    {
        isRopeMode = true;
    }
    
    // 由RopeSystem调用，退出钩索模式
    public void ExitRopeMode()
    {
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
}
