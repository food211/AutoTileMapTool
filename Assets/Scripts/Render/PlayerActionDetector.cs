using UnityEngine;

public class PlayerActionDetector : MonoBehaviour
{
  [Header("检测设置")]
  [SerializeField] private float landingImpactMultiplier = 0.4f;    // 降低落地冲击倍数
  [SerializeField] private float movementImpactMultiplier = 0.15f;  // 降低移动冲击倍数
  [SerializeField] private float ropeReleaseImpactMultiplier = 0.2f; // 降低绳索释放冲击倍数
  [SerializeField] private float maxImpactStrength = 1f;          // 降低最大冲击强度
  [SerializeField] private float impactRadius = 4f;                 // 影响半径
  [Header("冷却设置")]
[SerializeField] private float movementImpactCooldown = 0.1f;
[SerializeField] private float jumpingImpactCooldown = 0.05f;
[SerializeField] private float landingImpactCooldown = 0.0f;  // 落地不需要额外冷却
    [SerializeField] private float ropeReleaseImpactCooldown = 0.0f;  // 绳索释放不需要额外冷却
// 跟踪上次触发时间
private float lastMovementImpactTime = -100f;
private float lastJumpingImpactTime = -100f;
private float lastLandingImpactTime = -100f;
private float lastRopeReleaseImpactTime = -100f;

  [Header("调试信息")]
  [SerializeField] private bool showDebugInfo = false;
  [SerializeField] private float currentSpeed;
  [SerializeField] private float lastImpactStrength;
  [SerializeField] private string lastImpactSource;
  
  // 组件引用
  private PlayerController playerController;
  private Rigidbody2D playerRigidbody;
  
  // 状态跟踪
  private Vector3 lastPosition;
  private Vector3 lastVelocity;
  private bool wasGrounded = true;
  private bool wasInRopeMode = false;
  private bool isPlayerJumping = false;
  private float lastGroundTime;
  private float lastRopeReleaseTime;
  
  // 缓存反射字段，避免重复获取
  private System.Reflection.FieldInfo isGroundedField;
  private System.Reflection.FieldInfo isRopeModeField;
  private System.Reflection.FieldInfo moveSpeedField;
  private System.Reflection.FieldInfo isJumpingField;
  
  // 事件
  public System.Action<Vector3, float, float> OnPlayerImpact;  // 位置，强度，半径
  
  void Start()
  {
      // 获取PlayerController引用
      playerController = GetComponent<PlayerController>();
      if (playerController == null)
      {
          Debug.LogError("PlayerActionDetector需要与PlayerController在同一个GameObject上！");
          enabled = false;
          return;
      }
      
      // 获取Rigidbody2D引用
      playerRigidbody = GetComponent<Rigidbody2D>();
      if (playerRigidbody == null)
      {
          Debug.LogError("未找到Rigidbody2D组件！");
          enabled = false;
          return;
      }
      
      // 缓存反射字段
      CacheReflectionFields();
      
      // 初始化位置和状态
      lastPosition = transform.position;
      lastVelocity = playerRigidbody.velocity;
      
      // 检查交互管理器
      if (PlantInteractionManager.Instance == null)
      {
          Debug.LogWarning("未找到 PlantInteractionManager，请确保场景中有该组件");
      }
      
      if (showDebugInfo)
      {
          Debug.Log("PlayerActionDetector 已初始化，连接到 PlayerController");
      }
  }
  void CacheReflectionFields()
  {
      var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
      
      try
      {
          isGroundedField = typeof(PlayerController).GetField("isGrounded", bindingFlags);
          isRopeModeField = typeof(PlayerController).GetField("isRopeMode", bindingFlags);
          moveSpeedField = typeof(PlayerController).GetField("moveSpeed", bindingFlags);
          isJumpingField = typeof(PlayerController).GetField("isJumping", bindingFlags);
          
          if (showDebugInfo)
          {
              Debug.Log($"反射字段缓存结果 - isGrounded: {isGroundedField != null}, isRopeMode: {isRopeModeField != null}, moveSpeed: {moveSpeedField != null}, isJumping: {isJumpingField != null}");
          }
      }
      catch (System.Exception e)
      {
          Debug.LogError($"缓存反射字段时出错: {e.Message}");
      }
  }
  
  void Update()
  {
      if (playerController == null || playerRigidbody == null) return;
      
      DetectPlayerActions();
      UpdateDebugInfo();
  }
  
  void DetectPlayerActions()
  {
      Vector3 currentPosition = transform.position;
      Vector3 currentVelocity = playerRigidbody.velocity;
      currentSpeed = currentVelocity.magnitude;
      
      // 获取PlayerController的状态
      bool isCurrentlyGrounded = GetPlayerGroundedState();
      bool isCurrentlyInRopeMode = GetPlayerRopeModeState();
      isPlayerJumping = GetPlayerJumpState();
      
      // 检测落地
    DetectLanding(currentVelocity, isCurrentlyGrounded);
      
      // 检测绳索释放
      DetectRopeRelease(isCurrentlyInRopeMode);
      
      // 检测快速移动
      DetectMovement(currentVelocity, isCurrentlyGrounded);
          // 添加：检测跳跃
    DetectJumping(currentVelocity);
      
      // 更新状态
        lastPosition = currentPosition;
      lastVelocity = currentVelocity;
      wasGrounded = isCurrentlyGrounded;
      wasInRopeMode = isCurrentlyInRopeMode;
  }
  
  
  // 修复后的反射方法
  bool GetPlayerGroundedState()
  {
      if (playerController == null) return false;
      
      try
      {
          if (isGroundedField != null)
          {
              return (bool)isGroundedField.GetValue(playerController); // 传递正确的实例
          }
      }
      catch (System.Exception e)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"获取isGrounded状态失败，使用备用检测: {e.Message}");
          }
      }
      
      // 备用方案：使用物理检测
      return Physics2D.OverlapBox(
          (Vector2)transform.position + new Vector2(0f, -0.05f),
          new Vector2(0.9f, 0.1f),
          0f,
          LayerMask.GetMask("Ground", "Platform", "Default") // 多个可能的地面层
      ) != null;
  }
  
  bool GetPlayerRopeModeState()
  {
      if (playerController == null) return false;
      
      try
      {
          if (isRopeModeField != null)
          {
              return (bool)isRopeModeField.GetValue(playerController); // 传递正确的实例
          }
      }
      catch (System.Exception e)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"获取isRopeMode状态失败: {e.Message}");
          }
      }
      
      return false;
  }
  
    bool GetPlayerJumpState()
  {
      if (playerController == null) return false;
      
      try
      {
          if (isJumpingField != null)
          {
              return (bool)isJumpingField.GetValue(playerController); // 传递正确的实例
          }
      }
      catch (System.Exception e)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"获取isJumping状态失败: {e.Message}");
          }
      }
      
      return false;
  }

  float GetPlayerMoveSpeed()
  {
      if (playerController == null) return 5f;
      
      try
      {
          if (moveSpeedField != null)
          {
              return (float)moveSpeedField.GetValue(playerController); // 传递正确的实例
          }
      }
      catch (System.Exception e)
      {
          if (showDebugInfo)
          {
              Debug.LogWarning($"获取moveSpeed失败，使用默认值: {e.Message}");
          }
      }
      
      return 5f; // 默认值
  }
  
  void DetectLanding(Vector3 currentVelocity, bool isCurrentlyGrounded)
{
    // 从空中落到地面
    if (isCurrentlyGrounded && !wasGrounded)
    {
        float impactForce = Mathf.Abs(lastVelocity.y);
        float horizontalSpeed = Mathf.Abs(lastVelocity.x);
        
        // 结合垂直和水平速度计算冲击
        float totalImpact = impactForce + (horizontalSpeed * 0.3f);
        
        // 提高阈值，从3f提高到5f
        if (totalImpact > 5f) 
        {
            float impactStrength = Mathf.Clamp01(totalImpact / 15f) * maxImpactStrength * landingImpactMultiplier;
            
            if (Time.time - lastLandingImpactTime > landingImpactCooldown)
            {
                TriggerImpact(impactStrength, "Landing");
                lastLandingImpactTime = Time.time;
            }
            
            // 如果有落地挤压效果，可以触发植物的相应效果
            if (totalImpact > 10f)  // 从8f提高到10f
            {
                TriggerImpact(impactStrength * 1.5f, "Hard Landing");
            }
        }
        
        lastGroundTime = Time.time;
    }
}
  
  void DetectRopeRelease(bool isCurrentlyInRopeMode)
  {
      // 从绳索模式切换到非绳索模式
      if (!isCurrentlyInRopeMode && wasInRopeMode)
      {
          float releaseSpeed = playerRigidbody.velocity.magnitude;
          
          if (releaseSpeed > 5f) // 绳索释放最小速度阈值
          {
              float impactStrength = Mathf.Clamp01(releaseSpeed / 20f) * maxImpactStrength * ropeReleaseImpactMultiplier;
                // 检查冷却时间
                if (Time.time - lastRopeReleaseImpactTime < ropeReleaseImpactCooldown)
                {
                    return;  // 还在冷却中，不触发
                }
              TriggerImpact(impactStrength, "Rope Release");
          }
          
          lastRopeReleaseTime = Time.time;
      }
  }
  
void DetectMovement(Vector3 currentVelocity, bool isCurrentlyGrounded)
{
    // 检查冷却时间
    if (Time.time - lastMovementImpactTime < movementImpactCooldown)
    {
        return;  // 还在冷却中，不触发
    }
    
    // 水平移动速度
    float horizontalSpeed = Mathf.Abs(currentVelocity.x);
    
    // 使用PlayerController的moveSpeed作为参考
    float moveSpeedThreshold = GetPlayerMoveSpeed() * 0.9f; // 提高阈值到90%的移动速度
    
    if (horizontalSpeed > moveSpeedThreshold)
    {
        // 基于速度和PlayerController的moveSpeed计算影响强度
        float speedRatio = horizontalSpeed / (GetPlayerMoveSpeed() * 2f);
        float impactStrength = Mathf.Clamp01(speedRatio) * maxImpactStrength * movementImpactMultiplier;
        
        // 根据是否在地面调整冲击强度和来源
        string impactSource = isCurrentlyGrounded ? "Fast Movement" : "Aerial Movement";
        
        // 如果在空中，可以稍微增强冲击效果
        if (!isCurrentlyGrounded)
        {
            impactStrength *= 1.2f; // 空中移动冲击略强
        }
        
        // 避免频繁触发，添加时间间隔
        if (Time.time - lastGroundTime > 0.1f && impactStrength > 0.15f) // 添加最小强度阈值
        {
            TriggerImpact(impactStrength, impactSource);
            lastMovementImpactTime = Time.time;  // 更新上次触发时间
        }
    }
}

  void DetectJumping(Vector3 currentVelocity)
{
    // 检查冷却时间
    if (Time.time - lastJumpingImpactTime < jumpingImpactCooldown)
    {
        return;  // 还在冷却中，不触发
    }
    
    // 如果玩家正在跳跃且有足够的速度
    if (isPlayerJumping)
    {
        // 计算总速度（垂直+水平）
        float verticalSpeed = Mathf.Abs(currentVelocity.y);
        float horizontalSpeed = Mathf.Abs(currentVelocity.x);
        float totalSpeed = verticalSpeed + (horizontalSpeed * 0.5f);
        
        // 提高阈值，只有当速度超过更高阈值时才触发冲击
        if (totalSpeed > 8f)  // 从5f提高到8f
        {
            // 根据速度计算冲击强度，使用与落地相似但略低的系数
            float impactStrength = Mathf.Clamp01(totalSpeed / 15f) * maxImpactStrength * landingImpactMultiplier * 0.7f;
            
            // 避免频繁触发，添加时间间隔检查
            if (Time.time - lastGroundTime > 0.2f && impactStrength > 0.2f)  // 提高最小强度阈值
            {
                TriggerImpact(impactStrength, "Jumping");
                lastJumpingImpactTime = Time.time;  // 更新上次触发时间
            }
        }
    }
}
  
  void TriggerImpact(float strength, string source)
  {
      lastImpactStrength = strength;
      lastImpactSource = source;
      
      if (showDebugInfo)
      {
          Debug.Log($"植物影响触发 - 来源:{source}, 强度:{strength:F2}, 速度:{currentSpeed:F1}, 位置:{transform.position}");
      }
      
      // 通知交互管理器
      if (PlantInteractionManager.Instance != null)
      {
          PlantInteractionManager.Instance.OnPlayerImpact(transform.position, strength, impactRadius);
      }
      
      // 触发事件
      OnPlayerImpact?.Invoke(transform.position, strength, impactRadius);
  }
  
  void UpdateDebugInfo()
  {
      if (!showDebugInfo) return;
      
      // 更新当前速度显示
      currentSpeed = playerRigidbody.velocity.magnitude;
  }
  
  // 手动触发影响（用于测试或特殊情况）
  public void ManualTriggerImpact(float strength)
  {
      TriggerImpact(strength, "Manual");
  }
  
  // 公共方法：获取当前状态信息
  public bool IsGrounded() => GetPlayerGroundedState();
  public bool IsInRopeMode() => GetPlayerRopeModeState();
  public float GetCurrentSpeed() => currentSpeed;
  public Vector3 GetCurrentVelocity() => playerRigidbody != null ? playerRigidbody.velocity : Vector3.zero;
  
  void OnDrawGizmosSelected()
  {
      if (!showDebugInfo || !Application.isPlaying) return;
      
      // 绘制影响范围
      Gizmos.color = Color.green;
      Gizmos.DrawWireSphere(transform.position, impactRadius);
      
      // 绘制速度向量
      if (playerRigidbody != null)
      {
          Gizmos.color = Color.blue;
          Gizmos.DrawRay(transform.position, playerRigidbody.velocity * 0.2f);
      }
      
      // 绘制地面检测区域（只在运行时且playerController不为null时）
      if (playerController != null)
      {
          bool isGrounded = GetPlayerGroundedState();
          Gizmos.color = isGrounded ? Color.green : Color.red;
          Vector3 checkPos = transform.position + new Vector3(0f, -0.05f, 0f);
          Gizmos.DrawWireCube(checkPos, new Vector3(0.9f, 0.1f, 0f));
      }
  }
  
  void OnGUI()
  {
      if (!showDebugInfo || !Application.isPlaying) return;
      
      GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 200));
      GUILayout.Label("=== 玩家动作检测 ===");
      GUILayout.Label($"当前速度: {currentSpeed:F1}");
      GUILayout.Label($"是否着地: {GetPlayerGroundedState()}");
      GUILayout.Label($"绳索模式: {GetPlayerRopeModeState()}");
      GUILayout.Label($"最后冲击: {lastImpactSource}");
      GUILayout.Label($"冲击强度: {lastImpactStrength:F2}");
      
      if (GUILayout.Button("测试冲击"))
      {
          ManualTriggerImpact(1f);
      }
      
      GUILayout.EndArea();
  }
}