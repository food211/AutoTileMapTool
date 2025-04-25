using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// 状态管理器 - 负责处理玩家的各种状态效果

public class StatusManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private SpriteRenderer playerRenderer;
    
    [Header("状态效果设置")]
    [SerializeField] private float frozenDuration = 3.0f;
    [SerializeField] private float electrifiedDuration = 2.0f;
    [SerializeField] private int burningDamage = 1;
    [SerializeField] private float burningDrag = 0.8f;
    [SerializeField] private float burningDamageInterval = 1.5f;
    [SerializeField] private int struggletimes = 20;
    
    [Header("视觉效果")]
    [SerializeField] private GameObject frozenEffect;
    [SerializeField] private GameObject burningEffect;
    [SerializeField] private GameObject electrifiedEffect;
    public Color frozenTint = new Color(0.7f, 0.7f, 1.0f);
    public Color burningTint = new Color(1.0f, 0.6f, 0.6f);
    private Color electrifiedTint = new Color(1.0f, 1.0f, 0.6f);

    [Header("冷却时间设置")]
    [SerializeField] private float frozenCooldown = 2.0f; // 冰冻状态结束后的冷却时间
    [SerializeField] private float electrifiedCooldown = 1.5f; // 电击状态结束后的冷却时间

    // 内部变量
    private GameEvents.PlayerState currentState = GameEvents.PlayerState.Normal;
    private Color originalTint;
    private float originalDrag;
    private float originalGravityScale;
    private bool originalKinematic;
    private Vector2 originalVelocity;
    private bool isFrozenOnCooldown = false;
    private bool isElectrifiedOnCooldown = false;
    public bool ropeIsBurning = false;
    [SerializeField] [Tooltip("玩家是否燃烧")] private bool isPlayerBurn = false;
    
    // 协程引用
    private Coroutine currentStatusCoroutine;
    
    private void Awake()
    {
        // 获取组件引用
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
            
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();
            
        if (playerRenderer == null)
            playerRenderer = GetComponent<SpriteRenderer>();

        if (ropeSystem == null)
        ropeSystem = playerController.GetComponentInChildren<RopeSystem>();
            
        // 保存原始值
        if (playerRenderer != null)
            originalTint = playerRenderer.color;
            
        if (playerRigidbody != null)
        {
            originalDrag = playerRigidbody.drag;
            originalGravityScale = playerRigidbody.gravityScale;
            originalKinematic = playerRigidbody.isKinematic;
        }
    }
#region Events
    private void OnEnable()
    {
        // 注册事件监听
        GameEvents.OnPlayerStateChanged += OnPlayerStateChanged;
        GameEvents.OnPlayerBurningStateChanged += SetPlayerBurn;
    }
    
    private void OnDisable()
    {
        // 取消注册事件监听
        GameEvents.OnPlayerStateChanged -= OnPlayerStateChanged;
        GameEvents.OnPlayerBurningStateChanged -= SetPlayerBurn;
        
        // 确保协程被停止
        if (currentStatusCoroutine != null)
        {
            StopCoroutine(currentStatusCoroutine);
            currentStatusCoroutine = null;
        }
    }
#endregion
#region State Change
    private void OnPlayerStateChanged(GameEvents.PlayerState newState)
    {
        // 如果状态没有变化，直接返回
        if (currentState == newState) return;
        
        // 记录前一个状态
        GameEvents.PlayerState previousState = currentState;
        currentState = newState;
        
        // 在控制台输出状态变化信息（调试用）
        #if UNITY_EDITOR
        Debug.LogFormat($"玩家状态从 {previousState} 变为 {newState}");
        #endif
        
        // 停止当前正在运行的状态协程
        if (currentStatusCoroutine != null)
        {
            StopCoroutine(currentStatusCoroutine);
            currentStatusCoroutine = null;
        }
        
        // 恢复默认设置，然后应用新状态的效果
        ResetToDefault();
        
        // 根据新状态应用相应的效果
        switch (newState)
        {
            case GameEvents.PlayerState.Normal:
                break;
                
            case GameEvents.PlayerState.Swinging:
                break;
                
            case GameEvents.PlayerState.Frozen:
                currentStatusCoroutine = StartCoroutine(ApplyFrozenState());
                break;
                
            case GameEvents.PlayerState.Burning:
                currentStatusCoroutine = StartCoroutine(ApplyBurningState());
                break;
                
            case GameEvents.PlayerState.Electrified:
                currentStatusCoroutine = StartCoroutine(ApplyElectrifiedState());
                break;
        }
    }
    
    
    /// 重置所有设置到默认状态
    
    private void ResetToDefault()
    {
        // 恢复玩家输入控制
        if (playerController != null)
            playerController.SetPlayerInput(true);
            
        // 恢复原始物理属性
        if (playerRigidbody != null)
        {
            playerRigidbody.drag = originalDrag;
            playerRigidbody.gravityScale = originalGravityScale;
            playerRigidbody.isKinematic = originalKinematic;
        }
        
        // 恢复原始视觉效果
        if (playerRenderer != null)
            playerRenderer.color = originalTint;
            
        // 关闭所有特效
        if (frozenEffect != null) frozenEffect.SetActive(false);
        if (burningEffect != null) burningEffect.SetActive(false);
        if (electrifiedEffect != null) electrifiedEffect.SetActive(false);
    }
    
    
    /// 应用冰冻状态
    
    private IEnumerator ApplyFrozenState()
    {
        // 保存当前速度
        if (playerRigidbody != null)
            originalVelocity = playerRigidbody.velocity;
        
        // 禁用玩家输入
        if (playerController != null)
            playerController.SetPlayerInput(false);
            GameEvents.TriggerCanShootRopeChanged(false);
            
        // 应用冰冻视觉效果
        if (playerRenderer != null)
            playerRenderer.color = frozenTint;
            
        // 激活冰冻特效
        if (frozenEffect != null)
            frozenEffect.SetActive(true);

        // 冻结玩家
        playerRigidbody.velocity = Vector2.zero;
        playerRigidbody.isKinematic = true;

        // 播放冰冻音效
        // PlaySound(frozenSound);
        
        // 等待冰冻持续时间
        yield return new WaitForSeconds(frozenDuration);
        
        // 如果状态仍然是冰冻，恢复正常状态
        if (currentState == GameEvents.PlayerState.Frozen)
        {
            // 恢复物理属性
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = originalKinematic;
                playerRigidbody.velocity = originalVelocity * 0.1f; // 恢复一部分速度
            }
            
            // 触发状态变化事件，回到正常状态或摆动状态
            if (playerController != null && playerController.IsInRopeMode())
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            }
            else
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            }
            // 开始冰冻冷却时间
            StartCoroutine(FrozenCooldownRoutine());
        }
    }
    
    
   #region 应用燃烧状态
    
private IEnumerator ApplyBurningState()
{
    // 应用燃烧视觉效果
    if (playerRenderer != null && isPlayerBurn)
        playerRenderer.color = burningTint;

    // 禁用玩家松开绳子
    if (playerController != null)
        GameEvents.TriggerCanShootRopeChanged(false);
        
    // 激活燃烧特效
    if (burningEffect != null)
        burningEffect.SetActive(true);
        
    // 应用燃烧物理效果
    if (playerRigidbody != null)
    {
        playerRigidbody.drag = burningDrag;
    }
    
    // 获取绳索系统组件
    if (ropeSystem != null && ropeSystem.HasAnchors())
    {
        // 开始绳索燃烧效果
        ropeIsBurning = true;
        StartCoroutine(BurnRopeEffect(ropeSystem));
    }
    
    // 燃烧持续伤害
    float elapsedTime = 0f;
    float nextDamageTime = 0f;
    
    // 玩家离开火物体后的燃烧计时器
    float playerBurnTimer = 0f;
    float maxPlayerBurnTime = 4.0f; // 最大燃烧时间，4秒
    bool wasPlayerBurning = false;
    
    // 只要玩家处于燃烧状态或绳索在燃烧，就继续循环
    while (currentState == GameEvents.PlayerState.Burning && 
           (isPlayerBurn || wasPlayerBurning || ropeIsBurning))
    {
        elapsedTime += Time.deltaTime;
        
        // 检查玩家燃烧状态变化
        if (isPlayerBurn)
        {
            // 玩家正在接触火物体，重置计时器
            playerBurnTimer = 0f;
            wasPlayerBurning = true;
            
            // 应用燃烧视觉效果（确保在接触火物体时始终显示燃烧效果）
            if (playerRenderer != null)
                playerRenderer.color = burningTint;
                
            // 激活燃烧特效
            if (burningEffect != null && !burningEffect.activeSelf)
                burningEffect.SetActive(true);
        }
        else if (wasPlayerBurning)
        {
            // 玩家不再接触火物体，但之前在燃烧
            playerBurnTimer += Time.deltaTime;
            
            // 如果超过最大燃烧时间，停止玩家燃烧
            if (playerBurnTimer >= maxPlayerBurnTime)
            {
                wasPlayerBurning = false;
                
                // 关闭燃烧特效
                if (burningEffect != null)
                    burningEffect.SetActive(false);
                    
                // 恢复原始颜色
                if (playerRenderer != null)
                    playerRenderer.color = originalTint;
            }
        }
        
        // 定期造成伤害 - 只在玩家燃烧时造成伤害
        if ((isPlayerBurn || wasPlayerBurning) && elapsedTime >= nextDamageTime)
        {
            // 造成伤害
            ApplyDamage(burningDamage);
            
            // 闪烁效果
            if (playerRenderer != null)
            {
                playerRenderer.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                playerRenderer.color = isPlayerBurn || wasPlayerBurning ? burningTint : originalTint;
            }
            
            // 设置下一次伤害时间
            nextDamageTime = elapsedTime + burningDamageInterval;
        }
        
        // 检查是否应该退出循环 - 玩家不再燃烧且绳索也不再燃烧
        if (!isPlayerBurn && !wasPlayerBurning && !ropeIsBurning)
        {
            break;
        }
        
        yield return null;
    }
    
    // 如果状态仍然是燃烧，恢复正常状态
    if (currentState == GameEvents.PlayerState.Burning)
    {
        // 重置绳索燃烧状态
        ropeIsBurning = false;
        
        // 触发状态变化事件，断开绳子回到正常状态
        if (playerController != null && playerController.IsInRopeMode())
        {
            GameEvents.TriggerRopeReleased();
        }
        
        // 无论是否在绳索模式，都重置玩家燃烧状态
        GameEvents.TriggerCanShootRopeChanged(true);
        GameEvents.TriggerSetPlayerBurning(false);
        GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
    }
}

private IEnumerator BurnRopeEffect(RopeSystem ropeSystem)
{
    // 初始化燃烧效果
    LineRenderer lineRenderer = ropeSystem.GetComponent<LineRenderer>();
    if (lineRenderer == null) yield break;
    
    // 保存原始宽度和颜色
    float originalStartWidth = lineRenderer.startWidth;
    float originalEndWidth = lineRenderer.endWidth;
    Color originalColor = lineRenderer.startColor;
    
    // 获取燃烧起始锚点索引
    int burnAnchorIndex = ropeSystem.GetBurningAnchorIndex();
    if (burnAnchorIndex < 0) burnAnchorIndex = 0;
    
    // 创建火焰粒子效果
    GameObject fireParticle = ropeSystem.CreateFireParticle();
    
    // 燃烧进度
    float burnProgress = 0f;
    float burnSpeed = ropeSystem.GetBurnPropagationSpeed();
    float burnThreshold = ropeSystem.GetBurnBreakThreshold();
    
    // 获取锚点列表
    List<Vector2> anchors = ropeSystem.GetAnchors();
    
    // 保存原始宽度曲线
    AnimationCurve originalWidthCurve = null;
    if (lineRenderer.widthCurve != null)
    {
        // 复制原始曲线的所有关键帧
        originalWidthCurve = new AnimationCurve();
        foreach (Keyframe key in lineRenderer.widthCurve.keys)
        {
            originalWidthCurve.AddKey(key);
        }
    }
    else
    {
        // 如果没有原始曲线，创建一个简单的线性曲线
        originalWidthCurve = new AnimationCurve();
        originalWidthCurve.AddKey(0f, originalStartWidth);
        originalWidthCurve.AddKey(1f, originalEndWidth);
    }
    
    while (burnProgress < 1.0f && currentState == GameEvents.PlayerState.Burning && ropeSystem.HasAnchors())
    {
        burnProgress += Time.deltaTime * burnSpeed / 10f; // 调整为适当的燃烧速度
        
        // 更新线渲染器颜色和宽度
        if (lineRenderer != null)
        {
            // 从燃烧点开始向两端传播
            int pointCount = lineRenderer.positionCount;
            
            // 计算燃烧点在线渲染器中的索引
            int burnPointIndex = burnAnchorIndex + 1;
            
            // 计算每个点的燃烧程度和颜色
            Color[] colors = new Color[pointCount];
            
            // 定义颜色
            Color fireColor = new Color(1.0f, 0.3f, 0.1f); // 火红色
            Color charColor = new Color(0.1f, 0.1f, 0.1f); // 烧焦的黑色
            
            // 计算影响范围
            float burnRange = 0.3f; // 燃烧影响的范围比例
            float charRange = 0.1f; // 烧焦影响的范围比例（应小于burnRange）
            
            for (int i = 0; i < pointCount; i++)
            {
                // 计算该点到燃烧起始点的归一化距离
                float normalizedDistance;
                
                if (pointCount <= 1)
                {
                    normalizedDistance = 0;
                }
                else
                {
                    float burnPointPos = (float)burnPointIndex / (pointCount - 1);
                    float pointPos = (float)i / (pointCount - 1);
                    normalizedDistance = Mathf.Abs(pointPos - burnPointPos);
                }
                
                // 根据距离和燃烧进度计算颜色
                if (normalizedDistance < charRange * burnProgress)
                {
                    // 最中心的烧焦部分 - 黑色
                    float charIntensity = 1.0f - (normalizedDistance / (charRange * burnProgress));
                    colors[i] = Color.Lerp(fireColor, charColor, charIntensity * burnProgress);
                }
                else if (normalizedDistance < burnRange)
                {
                    // 燃烧部分 - 红色到橙色渐变
                    float fireIntensity = 1.0f - ((normalizedDistance - (charRange * burnProgress)) / (burnRange - (charRange * burnProgress)));
                    fireIntensity = Mathf.Clamp01(fireIntensity * burnProgress);
                    colors[i] = Color.Lerp(originalColor, fireColor, fireIntensity);
                }
                else
                {
                    // 远离燃烧点的部分保持原色
                    colors[i] = originalColor;
                }
            }
            
            // 复制原始宽度曲线
            AnimationCurve widthCurve = new AnimationCurve();
            foreach (Keyframe key in originalWidthCurve.keys)
            {
                widthCurve.AddKey(key);
            }
            
            // 计算燃烧点的归一化位置
            float burnPointPosition = (float)burnPointIndex / (pointCount - 1);
            
            // 燃烧点宽度从原始宽度逐渐变细到0.01
            float minWidth = 0.01f;
            float burnWidth = Mathf.Lerp(originalEndWidth, minWidth, burnProgress * 0.8f);
            
            // 在燃烧点位置添加关键帧
            // 先检查是否已经有接近该位置的关键帧
            bool keyExists = false;
            for (int i = 0; i < widthCurve.keys.Length; i++)
            {
                if (Mathf.Abs(widthCurve.keys[i].time - burnPointPosition) < 0.01f)
                {
                    // 如果已有关键帧，更新它
                    Keyframe existingKey = widthCurve.keys[i];
                    existingKey.value = burnWidth;
                    widthCurve.MoveKey(i, existingKey);
                    keyExists = true;
                    break;
                }
            }
            
            // 如果没有找到接近的关键帧，添加新的
            if (!keyExists)
            {
                int keyIndex = widthCurve.AddKey(burnPointPosition, burnWidth);
                
                // 设置切线为0，使曲线在该点有尖锐的变化
                if (keyIndex >= 0)
                {
                    Keyframe newKey = widthCurve.keys[keyIndex];
                    newKey.inTangent = 0;
                    newKey.outTangent = 0;
                    widthCurve.MoveKey(keyIndex, newKey);
                }
            }
            
            // 应用颜色
            lineRenderer.colorGradient = CreateGradient(colors);
            
            // 应用宽度曲线
            lineRenderer.widthCurve = widthCurve;
            
            // 更新火焰粒子位置
            if (fireParticle != null)
            {
                // 计算火焰位置 - 在燃烧最严重的部分
                Vector3 firePosition = lineRenderer.GetPosition(burnPointIndex);
                fireParticle.transform.position = firePosition;
            }
        }
        
        // 检查是否达到断开阈值
        if (burnProgress >= burnThreshold)
        {
            // 从燃烧点断开绳索
            ropeSystem.BreakRopeAtBurningPoint();
            
            // 触发绳索释放事件
            GameEvents.TriggerRopeReleased();
            GameEvents.TriggerCanShootRopeChanged(true);
            GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            
            // 销毁火焰粒子
            if (fireParticle != null)
            {
                Destroy(fireParticle, 1.0f); // 1秒后销毁，让效果自然消失
            }
            
            break;
        }
        
        yield return null;
    }
    
    // 恢复原始外观
    if (lineRenderer != null && ropeSystem.HasAnchors())
    {
        // 重置宽度 - 使用原始宽度曲线
        lineRenderer.widthCurve = ropeSystem.getOriginalCurve(); // 使用RopeSystem中保存的原始曲线
        lineRenderer.startWidth = originalStartWidth;
        lineRenderer.endWidth = originalEndWidth;
        
        // 恢复原始颜色
        lineRenderer.startColor = originalColor;
        lineRenderer.endColor = originalColor;
        lineRenderer.colorGradient = ropeSystem.getOriginalColorGradiant();
    }
    
    // 销毁火焰粒子
    if (fireParticle != null)
    {
        Destroy(fireParticle);
    }
}

// 创建颜色渐变
private Gradient CreateGradient(Color[] colors)
{
    Gradient gradient = new Gradient();
    GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
    GradientAlphaKey[] alphaKeys = new GradientAlphaKey[colors.Length];
    
    for (int i = 0; i < colors.Length; i++)
    {
        float time = (float)i / (colors.Length - 1);
        colorKeys[i] = new GradientColorKey(colors[i], time);
        alphaKeys[i] = new GradientAlphaKey(colors[i].a, time);
    }
    
    gradient.SetKeys(colorKeys, alphaKeys);
    return gradient;
}
#endregion    
#region 应用电击状态

private Coroutine electrifiedBlinkCoroutine;
private IEnumerator ApplyElectrifiedState()
{
    // 保存当前速度
    if (playerRigidbody != null)
        originalVelocity = playerRigidbody.velocity;

    // 判断是否是直接钩中带电物体
    bool isHookingElectrified = playerController != null && playerController.IsHookingElectrifiedObject();
    
    // 禁用玩家输入和发射绳索能力
    if (playerController != null)
    {
        playerController.SetPlayerInput(false);
        GameEvents.TriggerCanShootRopeChanged(false);
    }
        
    // 应用电击视觉效果
    if (playerRenderer != null)
        playerRenderer.color = electrifiedTint;
        
    // 激活电击特效
    if (electrifiedEffect != null)
        electrifiedEffect.SetActive(true);
        
    // 播放电击音效
    // PlaySound(electrifiedSound);

    playerRigidbody.velocity = Vector2.zero;
    // 保存原始位置
    Vector3 originalPosition = transform.position;
    
    // 记录开始时间，而不是累加时间
    float startTime = Time.time;
    float lastDamageTime = startTime;
    float lastVisualUpdateTime = startTime;
    
    // 挣脱计数器 - 仅在直接钩中带电物体时使用
    int struggleCount = 0;
    int requiredStruggles = struggletimes; // 需要按10次空格键才能挣脱
    bool keyWasPressed = false; // 用于跟踪上一帧是否按下了空格键
    
    // 启动单独的视觉效果协程
    StartCoroutine(ElectrifiedVisualEffects());
    
    // 如果是直接钩中带电物体，则持续电击直到挣脱；否则持续到electrifiedDuration结束
    while (((isHookingElectrified && struggleCount < requiredStruggles) || 
           (!isHookingElectrified && Time.time - startTime < electrifiedDuration)) && 
           currentState == GameEvents.PlayerState.Electrified)
    {
        // 检测空格键输入 - 仅在直接钩中带电物体时
        if (isHookingElectrified)
        {
            // 使用GetKeyDown而不是GetKeyUp，提高响应性
            if (Input.GetKeyDown(KeyCode.Space) && !keyWasPressed)
            {
                keyWasPressed = true;
                struggleCount++;
                
                // 显示挣脱进度提示
                Debug.LogFormat($"挣脱进度: {struggleCount}/{requiredStruggles}");
                
                // 可以在这里添加挣脱进度的视觉反馈
                if (playerRenderer != null)
                {
                    // 挣脱时闪烁一下白色
                    playerRenderer.color = Color.white;
                }
            }
            else if (!Input.GetKey(KeyCode.Space))
            {
                keyWasPressed = false;
            }
        }
        
        // 随机抖动效果 - 降低频率以减少性能开销
        if (Time.time - lastVisualUpdateTime > 0.05f)
        {
            if (playerRigidbody != null)
            {
                // 添加随机力模拟抖动
                Vector2 randomForce = new Vector2(
                    Random.Range(-5f, 5f),
                    Random.Range(-5f, 5f)
                );
                playerRigidbody.AddForce(randomForce, ForceMode2D.Impulse);
            }
            else
            {
                // 如果没有Rigidbody，直接移动Transform
                transform.position = originalPosition + new Vector3(
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.1f, 0.1f),
                    0
                );
            }
            
            lastVisualUpdateTime = Time.time;
        }
        
        // 更新原始位置（如果玩家在移动）
        originalPosition = new Vector3(transform.position.x, transform.position.y, originalPosition.z);
        
        // 如果是钩中状态，定期对玩家造成伤害（每2秒一次）
        float currentTime = Time.time;
        if (isHookingElectrified && currentTime - lastDamageTime >= 2.0f)
        {
            ApplyDamage(1);
            lastDamageTime = currentTime;
        }
        
        // 使用非常短的等待时间，以保持输入检测的高频率
        yield return new WaitForSeconds(0.01f);
    }
    
    // 如果状态仍然是电击，恢复正常状态
    if (currentState == GameEvents.PlayerState.Electrified)
    {
        // 恢复物理属性
        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = originalVelocity * 0.5f; // 恢复一半速度
        }
        
        // 恢复玩家输入控制
        if (playerController != null)
        {
            playerController.SetPlayerInput(true);
        }
        
        // 触发状态变化事件
        if (playerController != null && playerController.IsInRopeMode())
        {
            // 如果是直接钩中带电物体并成功挣脱，释放绳索
            if (isHookingElectrified && struggleCount >= requiredStruggles)
            {
                GameEvents.TriggerRopeReleased();
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
                
                // 启动麻痹状态协程，持续一段时间后恢复
                StartCoroutine(ApplyParalyzedState());
                
                // 可以添加一个成功挣脱的视觉或音效反馈
                Debug.LogFormat("成功挣脱电击！进入麻痹状态");
            }
            else if (!isHookingElectrified)
            {
                // 如果不是直接钩中带电物体，回到摆动状态
                GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Swinging);
                
                // 开始电击冷却时间
                StartCoroutine(ElectrifiedCooldownRoutine());
            }
            else
            {
                // 如果是直接钩中带电物体但没有成功挣脱，保持电击状态
                // 此处不做任何状态变更，玩家需要继续尝试挣脱
                
                // 重置挣脱计数，给玩家一个重新开始的机会
                struggleCount = 0;
                
                // 重新启动电击状态协程
                currentStatusCoroutine = StartCoroutine(ApplyElectrifiedState());
                yield break; // 提前返回，避免执行后续代码
            }
        }
        else
        {
            // 如果不在绳索模式，回到正常状态
            GameEvents.TriggerPlayerStateChanged(GameEvents.PlayerState.Normal);
            
            // 启动麻痹状态协程，持续一段时间后恢复
            StartCoroutine(ApplyParalyzedState());
        }
    }
}

// 新增方法：电击视觉效果协程 - 将视觉效果分离出来
private IEnumerator ElectrifiedVisualEffects()
{
    if (playerRenderer == null) yield break;
    
    // 闪烁颜色
    Color blinkColor1 = new Color(1.0f, 1.0f, 0.3f);
    Color blinkColor2 = electrifiedTint;
    
    // 持续闪烁直到状态改变
    while (currentState == GameEvents.PlayerState.Electrified)
    {
        // 闪烁效果
        playerRenderer.color = blinkColor1;
        yield return new WaitForSeconds(0.05f);
        playerRenderer.color = blinkColor2;
        yield return new WaitForSeconds(0.05f);
    }
}

// 新增方法：应用麻痹状态 - 取代等待落地的逻辑
private IEnumerator ApplyParalyzedState()
{
    // 设置电击冷却状态
    isElectrifiedOnCooldown = true;
    
    // 如果有PlayerController，通知它电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(true);
    }
    
    // 启动闪烁效果协程
    if (electrifiedBlinkCoroutine != null)
    {
        StopCoroutine(electrifiedBlinkCoroutine);
    }
    electrifiedBlinkCoroutine = StartCoroutine(ElectrifiedBlinkRoutine());
    
    // 应用麻痹效果 - 降低移动速度
    float originalMoveSpeed = 0f;
    if (playerController != null)
    {
        originalMoveSpeed = playerController.GetMoveSpeed();
        playerController.SetMoveSpeed(originalMoveSpeed * 0.5f); // 降低为原来的50%
    }
    
    // 禁用发射绳索能力
    GameEvents.TriggerCanShootRopeChanged(false);
    
    // 麻痹持续时间
    float paralysisDuration = 3.0f;
    float elapsedTime = 0f;
    
    Debug.LogFormat("玩家进入麻痹状态，持续 {0} 秒", paralysisDuration);
    
    // 等待麻痹时间结束
    while (elapsedTime < paralysisDuration)
    {
        elapsedTime += Time.deltaTime;
        yield return null;
    }
    
    // 麻痹结束，恢复正常状态
    if (playerController != null)
    {
        playerController.SetMoveSpeed(originalMoveSpeed); // 恢复原始移动速度
    }
    
    // 恢复发射绳索能力
    GameEvents.TriggerCanShootRopeChanged(true);
    
    // 停止闪烁效果
    if (electrifiedBlinkCoroutine != null)
    {
        StopCoroutine(electrifiedBlinkCoroutine);
        electrifiedBlinkCoroutine = null;
    }
    
    // 恢复原始颜色
    if (playerRenderer != null)
    {
        playerRenderer.color = originalTint;
    }
    
    // 冷却结束
    isElectrifiedOnCooldown = false;
    
    // 如果有PlayerController，取消电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(false);
    }
    
    Debug.LogFormat("麻痹状态结束，玩家恢复正常");
}

private IEnumerator ElectrifiedBlinkRoutine()
{
    if (playerRenderer == null) yield break;
    
    Color blinkColor1 = new Color(1.0f, 1.0f, 0.6f, 1.0f); // 淡黄色
    Color blinkColor2 = new Color(0.8f, 0.8f, 0.8f, 1.0f); // 淡灰色
    
    float blinkSpeed = 0.15f; // 闪烁速度
    
    while (true)
    {
        // 在两种颜色之间交替
        playerRenderer.color = blinkColor1;
        yield return new WaitForSeconds(blinkSpeed);
        playerRenderer.color = blinkColor2;
        yield return new WaitForSeconds(blinkSpeed);
    }
}
#endregion


#endregion
#region COOLDOWN
    private IEnumerator FrozenCooldownRoutine()
{
    // 设置冰冻冷却状态
    isFrozenOnCooldown = true;
    
    // 如果有PlayerController，通知它冰免疫状态
    if (playerController != null)
    {
        playerController.SetIceImmunity(true);
    }
    
    // 等待冷却时间
    yield return new WaitForSeconds(frozenCooldown);
    
    // 冷却结束
    isFrozenOnCooldown = false;
    
    // 如果有PlayerController，取消冰免疫状态
    if (playerController != null)
    {
        playerController.SetIceImmunity(false);
        GameEvents.TriggerCanShootRopeChanged(true);
    }
}

private IEnumerator ElectrifiedCooldownRoutine()
{
    // 设置电击冷却状态
    isElectrifiedOnCooldown = true;
    
    // 如果有PlayerController，通知它电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(true);
    }
    
    // 启动闪烁效果协程
    if (electrifiedBlinkCoroutine != null)
    {
        StopCoroutine(electrifiedBlinkCoroutine);
    }
    electrifiedBlinkCoroutine = StartCoroutine(ElectrifiedBlinkRoutine());
    
    // 等待冷却时间
    yield return new WaitForSeconds(electrifiedCooldown);
    
    // 冷却结束，恢复发射绳索能力
    if (playerController != null)
    {
        GameEvents.TriggerCanShootRopeChanged(true);
    }
    
    // 停止闪烁效果
    if (electrifiedBlinkCoroutine != null)
    {
        StopCoroutine(electrifiedBlinkCoroutine);
        electrifiedBlinkCoroutine = null;
    }
    
    // 恢复原始颜色
    if (playerRenderer != null)
    {
        playerRenderer.color = originalTint;
    }
    
    // 冷却结束
    isElectrifiedOnCooldown = false;
    
    // 如果有PlayerController，取消电击免疫状态
    if (playerController != null)
    {
        playerController.SetElectricImmunity(false);
    }
}
#endregion    
    
#region  应用伤害
    
    private void ApplyDamage(int damage)
    {
        // 这里可以连接到玩家的生命值系统
        // 例如：playerHealth.TakeDamage(damage);
        
        // 触发伤害事件
        GameEvents.TriggerPlayerDamaged(damage);
        
        // 视觉反馈
        StartCoroutine(DamageFlashRoutine());
    }
    
    
    /// 伤害闪烁效果
    
    private IEnumerator DamageFlashRoutine()
    {
        if (playerRenderer == null) yield break;
        
        Color originalColor = playerRenderer.color;
        
        // 闪烁3次
        for (int i = 0; i < 3; i++)
        {
            playerRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
#endregion
    public bool IsFrozenOnCooldown()
    {
    return isFrozenOnCooldown;
    }
    public void SetPlayerBurn(bool canBurn)
    {
        isPlayerBurn = canBurn;
    }

    public bool IsElectrifiedOnCooldown()
    {
        return isElectrifiedOnCooldown;
    }
        
        /// 获取当前玩家状态
        
        public GameEvents.PlayerState GetCurrentState()
        {
            return currentState;
        }
        
        
        /// 检查玩家是否处于特定状态
        
        public bool IsInState(GameEvents.PlayerState state)
        {
            return currentState == state;
        }
    }