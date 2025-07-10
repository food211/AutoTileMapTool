using System.Collections;
using UnityEngine;


/// 元素交互组件 - 处理游戏对象与冰、火、电等元素的交互
/// 用于非玩家对象，如箱子、障碍物等

public class ElementalInteractable : MonoBehaviour, IFreezable, IFlammable, IElectrifiable
{
    [Header("可视化效果")]
    [SerializeField] private SpriteRenderer objectRenderer;
    [SerializeField] private GameObject frozenEffectPrefab;
    [SerializeField] private GameObject burningEffectPrefab;
    [SerializeField] private GameObject electrifiedEffectPrefab;
    [SerializeField] private Color frozenTint = new Color(0.7f, 0.9f, 1.0f);
    [SerializeField] private Color burningTint = new Color(1.0f, 0.6f, 0.4f);
    [SerializeField] private Color electrifiedTint = new Color(0.9f, 0.9f, 0.5f);
    
    [Header("物理属性")]
    [SerializeField] private Rigidbody2D objectRigidbody;
    [SerializeField] private float frozenMassMultiplier = 1.5f; // 冰冻时质量增加
    [SerializeField] private float burningMassMultiplier = 0.8f; // 燃烧时质量减轻
    [SerializeField] private float frozenDragMultiplier = 2.0f; // 冰冻时阻力增加
    [SerializeField] private float burningDragMultiplier = 0.5f; // 燃烧时阻力减少
    
    [Header("元素持续时间")]
    [SerializeField] private float frozenDuration = 5.0f;
    [SerializeField] private float burningDuration = 4.0f;
    [SerializeField] private float electrifiedDuration = 3.0f;
    
    [Header("元素交互设置")]
    [SerializeField] private bool canFreeze = true;
    [SerializeField] private bool canBurn = true;
    [SerializeField] private bool canElectrify = true;
    [SerializeField] private bool destroyOnBurn = false; // 燃烧后是否销毁
    
    // 内部变量
    private Color originalColor;
    private float originalMass;
    private float originalDrag;
    private bool isFrozen = false;
    private bool isBurning = false;
    private bool isElectrified = false;
    
    // 当前活动的效果对象
    private GameObject activeFrozenEffect;
    private GameObject activeBurningEffect;
    private GameObject activeElectrifiedEffect;
    
    // 协程引用
    private Coroutine frozenCoroutine;
    private Coroutine burningCoroutine;
    private Coroutine electrifiedCoroutine;
    
    private void Awake()
    {
        // 获取组件引用
        if (objectRenderer == null)
            objectRenderer = GetComponent<SpriteRenderer>();
            
        if (objectRigidbody == null)
            objectRigidbody = GetComponent<Rigidbody2D>();
            
        // 保存原始值
        if (objectRenderer != null)
            originalColor = objectRenderer.color;
            
        if (objectRigidbody != null)
        {
            originalMass = objectRigidbody.mass;
            originalDrag = objectRigidbody.drag;
        }
    }
    
    #region 接口实现
    
    /// 实现IFreezable接口 - 处理与冰面接触
    
    public void OnIceContact(IceSurface iceSurface)
    {
        if (!canFreeze || isFrozen) return;
        
        // 如果正在燃烧，冰可以熄灭火
        if (isBurning)
        {
            StopBurning();
        }
        
        // 开始冰冻效果
        StartFreezing(iceSurface.GetFreezePower());
    }
    
    
    /// 实现IFlammable接口 - 处理与火焰接触
    
    public void OnFireContact(FireSurface fireSurface)
    {
        if (!canBurn || isBurning) return;
        
        // 如果已冰冻，火可以融化冰
        if (isFrozen)
        {
            StopFreezing();
        }
        
        // 开始燃烧效果
        StartBurning(fireSurface.GetFireDamage());
    }
    
    
    /// 实现IElectrifiable接口 - 处理与电击接触
    
    public void OnElectricContact(ElectricSurface electricSurface)
    {
        if (!canElectrify || isElectrified) return;
        
        // 开始电击效果
        StartElectrifying(electricSurface.GetElectricPower());
    }
    #endregion
    
    #region 元素效果处理
    
    /// 开始冰冻效果
    
    private void StartFreezing(float freezePower)
    {
        // 如果已经有冰冻协程在运行，先停止它
        if (frozenCoroutine != null)
        {
            StopCoroutine(frozenCoroutine);
        }
        
        // 开始新的冰冻协程
        frozenCoroutine = StartCoroutine(FrozenRoutine(freezePower));
    }
    
    
    /// 冰冻效果协程
    
    private IEnumerator FrozenRoutine(float freezePower)
    {
        // 设置冰冻状态
        isFrozen = true;
        
        // 应用视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = frozenTint;
        }
        
        // 创建冰冻特效
        if (frozenEffectPrefab != null && activeFrozenEffect == null)
        {
            activeFrozenEffect = Instantiate(frozenEffectPrefab, transform.position, Quaternion.identity);
            activeFrozenEffect.transform.SetParent(transform);
        }
        
        // 应用物理效果
        if (objectRigidbody != null)
        {
            // 保存当前速度
            Vector2 originalVelocity = objectRigidbody.velocity;
            
            // 增加质量和阻力，使物体更难移动
            objectRigidbody.mass = originalMass * frozenMassMultiplier;
            objectRigidbody.drag = originalDrag * frozenDragMultiplier;
            
            // 减少速度
            objectRigidbody.velocity = originalVelocity * 0.5f;
        }
        
        // 等待冰冻持续时间
        float duration = frozenDuration * (1 + freezePower * 0.1f); // 冰冻能力越强，持续时间越长
        yield return new WaitForSeconds(duration);
        
        // 恢复原状
        StopFreezing();
    }
    
    
    /// 停止冰冻效果
    
    private void StopFreezing()
    {
        // 如果有冰冻协程在运行，停止它
        if (frozenCoroutine != null)
        {
            StopCoroutine(frozenCoroutine);
            frozenCoroutine = null;
        }
        
        // 重置冰冻状态
        isFrozen = false;
        
        // 恢复视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = originalColor;
        }
        
        // 销毁冰冻特效
        if (activeFrozenEffect != null)
        {
            Destroy(activeFrozenEffect);
            activeFrozenEffect = null;
        }
        
        // 恢复物理属性
        if (objectRigidbody != null)
        {
            objectRigidbody.mass = originalMass;
            objectRigidbody.drag = originalDrag;
        }
    }
    
    
    /// 开始燃烧效果
    
    private void StartBurning(float fireDamage)
    {
        // 如果已经有燃烧协程在运行，先停止它
        if (burningCoroutine != null)
        {
            StopCoroutine(burningCoroutine);
        }
        
        // 开始新的燃烧协程
        burningCoroutine = StartCoroutine(BurningRoutine(fireDamage));
    }
    
    
    /// 燃烧效果协程
    
    private IEnumerator BurningRoutine(float fireDamage)
    {
        // 设置燃烧状态
        isBurning = true;
        
        // 应用视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = burningTint;
        }
        
        // 创建燃烧特效
        if (burningEffectPrefab != null && activeBurningEffect == null)
        {
            activeBurningEffect = Instantiate(burningEffectPrefab, transform.position, Quaternion.identity);
            activeBurningEffect.transform.SetParent(transform);
        }
        
        // 应用物理效果
        if (objectRigidbody != null)
        {
            // 减轻质量和阻力，使物体更容易移动
            objectRigidbody.mass = originalMass * burningMassMultiplier;
            objectRigidbody.drag = originalDrag * burningDragMultiplier;
        }
        
        // 随时间逐渐缩小物体（模拟燃烧消耗）
        Vector3 originalScale = transform.localScale;
        float elapsedTime = 0f;
        
        while (elapsedTime < burningDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // 缩小物体
            float scaleMultiplier = 1.0f - (elapsedTime / burningDuration) * 0.3f; // 最多缩小30%
            transform.localScale = originalScale * scaleMultiplier;
            
            // 闪烁效果
            if (objectRenderer != null && (int)(elapsedTime * 10) % 3 == 0)
            {
                objectRenderer.color = new Color(1.0f, 0.3f, 0.3f);
                yield return new WaitForSeconds(0.05f);
                objectRenderer.color = burningTint;
            }
            
            yield return null;
        }
        
        // 燃烧结束
        if (destroyOnBurn)
        {
            // 如果设置为燃烧后销毁，则销毁物体
            Destroy(gameObject);
        }
        else
        {
            // 否则停止燃烧效果
            StopBurning();
            
            // 燃烧后物体可能变小
            transform.localScale = originalScale * 0.8f;
        }
    }
    
    
    /// 停止燃烧效果
    
    private void StopBurning()
    {
        // 如果有燃烧协程在运行，停止它
        if (burningCoroutine != null)
        {
            StopCoroutine(burningCoroutine);
            burningCoroutine = null;
        }
        
        // 重置燃烧状态
        isBurning = false;
        
        // 恢复视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = originalColor;
        }
        
        // 销毁燃烧特效
        if (activeBurningEffect != null)
        {
            Destroy(activeBurningEffect);
            activeBurningEffect = null;
        }
        
        // 恢复物理属性
        if (objectRigidbody != null)
        {
            objectRigidbody.mass = originalMass;
            objectRigidbody.drag = originalDrag;
        }
    }
    
    
    /// 开始电击效果
    
    private void StartElectrifying(float electricPower)
    {
        // 如果已经有电击协程在运行，先停止它
        if (electrifiedCoroutine != null)
        {
            StopCoroutine(electrifiedCoroutine);
        }
        
        // 开始新的电击协程
        electrifiedCoroutine = StartCoroutine(ElectrifiedRoutine(electricPower));
    }
    
    
    /// 电击效果协程
    
    private IEnumerator ElectrifiedRoutine(float electricPower)
    {
        // 设置电击状态
        isElectrified = true;
        
        // 应用视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = electrifiedTint;
        }
        
        // 创建电击特效
        if (electrifiedEffectPrefab != null && activeElectrifiedEffect == null)
        {
            activeElectrifiedEffect = Instantiate(electrifiedEffectPrefab, transform.position, Quaternion.identity);
            activeElectrifiedEffect.transform.SetParent(transform);
        }
        
        // 保存原始位置
        Vector3 originalPosition = transform.position;
        
        // 电击抖动效果
        float elapsedTime = 0f;
        float duration = electrifiedDuration * (1 + electricPower * 0.05f); // 电击能力越强，持续时间越长
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            
            // 随机抖动效果
            if (objectRigidbody != null)
            {
                // 添加随机力模拟抖动
                Vector2 randomForce = new Vector2(
                    Random.Range(-5f, 5f),
                    Random.Range(-5f, 5f)
                ) * electricPower * 0.2f;
                
                objectRigidbody.AddForce(randomForce, ForceMode2D.Impulse);
            }
            else
            {
                // 如果没有Rigidbody，直接移动Transform
                transform.position = originalPosition + new Vector3(
                    Random.Range(-0.05f, 0.05f),
                    Random.Range(-0.05f, 0.05f),
                    0
                ) * electricPower * 0.1f;
            }
            
            // 闪烁效果
            if (objectRenderer != null)
            {
                objectRenderer.color = new Color(1.0f, 1.0f, 0.5f);
                yield return new WaitForSeconds(0.05f);
                objectRenderer.color = electrifiedTint;
            }
            
            yield return new WaitForSeconds(0.05f);
            
            // 更新原始位置（如果物体在移动）
            originalPosition = Vector3.Lerp(originalPosition, transform.position, 0.2f);
        }
        
        // 电击结束
        StopElectrifying();
    }
    
    
    /// 停止电击效果
    
    private void StopElectrifying()
    {
        // 如果有电击协程在运行，停止它
        if (electrifiedCoroutine != null)
        {
            StopCoroutine(electrifiedCoroutine);
            electrifiedCoroutine = null;
        }
        
        // 重置电击状态
        isElectrified = false;
        
        // 恢复视觉效果
        if (objectRenderer != null)
        {
            objectRenderer.color = originalColor;
        }
        
        // 销毁电击特效
        if (activeElectrifiedEffect != null)
        {
            Destroy(activeElectrifiedEffect);
            activeElectrifiedEffect = null;
        }
    }
    #endregion
    
    #region 公共方法
    
    /// 检查对象是否被冰冻
    
    public bool IsFrozen()
    {
        return isFrozen;
    }
    
    
    /// 检查对象是否在燃烧
    
    public bool IsBurning()
    {
        return isBurning;
    }
    
    
    /// 检查对象是否被电击
    
    public bool IsElectrified()
    {
        return isElectrified;
    }
    
    
    /// 设置是否可以被冰冻
    
    public void SetCanFreeze(bool value)
    {
        canFreeze = value;
    }
    
    
    /// 设置是否可以被燃烧
    
    public void SetCanBurn(bool value)
    {
        canBurn = value;
    }
    
    
    /// 设置是否可以被电击
    
    public void SetCanElectrify(bool value)
    {
        canElectrify = value;
    }
    #endregion
    
    private void OnDestroy()
    {
        // 确保所有特效都被清理
        if (activeFrozenEffect != null)
            Destroy(activeFrozenEffect);
            
        if (activeBurningEffect != null)
            Destroy(activeBurningEffect);
            
        if (activeElectrifiedEffect != null)
            Destroy(activeElectrifiedEffect);
    }
}