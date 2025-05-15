using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 相机效果管理器 - 提供预设的相机效果和动画
/// </summary>
public class CameraEffectsManager : MonoBehaviour
{
    [Header("相机引用")]
    [SerializeField] private CameraManager cameraManager;
    
    [Header("缩放效果设置")]
    [SerializeField] private float dramaticZoomInSize = 2.5f; // 戏剧性特写镜头大小
    [SerializeField] private float wideZoomOutSize = 10f; // 广角镜头大小
    [SerializeField] private float normalZoomSize = 5f; // 正常镜头大小
    [SerializeField] private float actionZoomSize = 4f; // 动作场景镜头大小
    
    [Header("震动效果设置")]
    [SerializeField] private float lightShakeAmount = 0.2f; // 轻微震动强度
    [SerializeField] private float mediumShakeAmount = 0.5f; // 中等震动强度
    [SerializeField] private float heavyShakeAmount = 0.8f; // 强烈震动强度
    [SerializeField] private float explosionShakeAmount = 1.0f; // 爆炸震动强度
    
    [Header("跟随设置")]
    [SerializeField] private float fastFollowSpeed = 0.3f; // 快速跟随速度
    [SerializeField] private float normalFollowSpeed = 0.1f; // 正常跟随速度
    [SerializeField] private float slowFollowSpeed = 0.05f; // 慢速跟随速度
    
    [Header("偏移设置")]
    [SerializeField] private Vector2 lookAheadOffset = new Vector2(2f, 0f); // 前视偏移
    [SerializeField] private Vector2 lookBackOffset = new Vector2(-2f, 0f); // 前视偏移
    [SerializeField] private Vector2 lookUpOffset = new Vector2(0f, 2f); // 上视偏移
    [SerializeField] private Vector2 lookDownOffset = new Vector2(0f, -2f); // 下视偏移
    [SerializeField] private Vector2 defaultOffset = Vector2.zero; // 默认偏移

    private Vector2 originalOffset; // 原始偏移值
    private float originalSmoothSpeed; // 原始平滑速度
    private Coroutine currentEffectCoroutine; // 当前效果协程

    private void Start()
    {
        // 如果没有指定相机管理器，尝试在同一游戏对象上查找
        if (cameraManager == null)
        {
            cameraManager = GetComponent<CameraManager>();

            // 如果还是找不到，尝试在场景中查找
            if (cameraManager == null)
            {
                cameraManager = FindObjectOfType<CameraManager>();

                if (cameraManager == null)
                {
                    Debug.LogError("CameraEffectsManager无法找到CameraManager组件！");
                }
            }
        }
        normalFollowSpeed = cameraManager.GetSmoothSpeed();
        // 保存原始设置
        if (cameraManager != null)
        {
            originalSmoothSpeed = normalFollowSpeed;
        }
        // 初始化原始偏移
        originalOffset = defaultOffset;
    }

    #region 公共效果方法

    /// <summary>
    /// 戏剧性特写效果 - 缩放到角色特写并轻微震动
    /// </summary>
    public void DramaticCloseUp(float duration = 2f)
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 启动新效果
        currentEffectCoroutine = StartCoroutine(DramaticCloseUpCoroutine(duration));
    }
    
    /// <summary>
    /// 战斗模式效果 - 适当缩小视野并增加跟随速度
    /// </summary>
    public void CombatMode(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 设置战斗模式相机参数
        cameraManager.ZoomTo(actionZoomSize);
        cameraManager.SetSmoothSpeed(fastFollowSpeed);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 探索模式效果 - 放大视野并减慢跟随速度
    /// </summary>
    public void ExplorationMode(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 设置探索模式相机参数
        cameraManager.ZoomTo(wideZoomOutSize);
        cameraManager.SetSmoothSpeed(slowFollowSpeed);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 环顾场景效果 - 广角视野并缓慢移动
    /// </summary>
    public void ScanEnvironment(float duration = 5f)
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 启动新效果
        currentEffectCoroutine = StartCoroutine(ScanEnvironmentCoroutine(duration));
    }
    
    /// <summary>
    /// 爆炸效果 - 强烈震动并短暂缩放
    /// </summary>
    public void ExplosionEffect()
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 启动新效果
        currentEffectCoroutine = StartCoroutine(ExplosionEffectCoroutine());
    }
    
    /// <summary>
    /// 跟随前方效果 - 相机偏移到角色前方
    /// </summary>
    public void LookAhead(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始偏移
        originalOffset = GetCurrentOffset();
        
        // 设置前视偏移
        cameraManager.SetOffset(lookAheadOffset);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetOffsetAfterDelay(duration));
        }
    }

        public void LookBack(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始偏移
        originalOffset = GetCurrentOffset();
        
        // 设置前视偏移
        cameraManager.SetOffset(lookBackOffset);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetOffsetAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 向上看效果 - 相机偏移到角色上方
    /// </summary>
    public void LookUp(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始偏移
        originalOffset = GetCurrentOffset();
        
        // 设置上视偏移
        cameraManager.SetOffset(lookUpOffset);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetOffsetAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 向下看效果 - 相机偏移到角色下方
    /// </summary>
    public void LookDown(float duration = 0f) // 0表示无限持续
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始偏移
        originalOffset = GetCurrentOffset();
        
        // 设置下视偏移
        cameraManager.SetOffset(lookDownOffset);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetOffsetAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 轻微震动效果
    /// </summary>
    public void LightShake()
    {
        cameraManager.ShakeCamera(lightShakeAmount);
    }
    
    /// <summary>
    /// 中等震动效果
    /// </summary>
    public void MediumShake()
    {
        cameraManager.ShakeCamera(mediumShakeAmount);
    }
    
    /// <summary>
    /// 强烈震动效果
    /// </summary>
    public void HeavyShake()
    {
        cameraManager.ShakeCamera(heavyShakeAmount);
    }
    
    /// <summary>
    /// 恢复默认相机设置
    /// </summary>
    public void ResetToDefault()
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 重置所有相机参数
        cameraManager.ResetZoom();
        cameraManager.SetSmoothSpeed(normalFollowSpeed);
        cameraManager.SetOffset(defaultOffset);
        cameraManager.StopAllShakesImmediately();
    }
    
    /// <summary>
    /// 设置自定义缩放大小
    /// </summary>
    public void SetCustomZoom(float zoomSize, float duration = 0f)
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 设置缩放
        cameraManager.ZoomTo(zoomSize);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetZoomAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 设置自定义跟随速度
    /// </summary>
    public void SetCustomFollowSpeed(float speed, float duration = 0f)
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始速度
        originalSmoothSpeed = GetCurrentSmoothSpeed();
        
        // 设置新速度
        cameraManager.SetSmoothSpeed(speed);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetSpeedAfterDelay(duration));
        }
    }
    
    /// <summary>
    /// 设置自定义偏移
    /// </summary>
    public void SetCustomOffset(Vector2 offset, float duration = 0f)
    {
        // 停止当前效果
        StopCurrentEffect();
        
        // 保存原始偏移
        originalOffset = GetCurrentOffset();
        
        // 设置新偏移
        cameraManager.SetOffset(offset);
        
        // 如果指定了持续时间，启动计时器
        if (duration > 0)
        {
            currentEffectCoroutine = StartCoroutine(ResetOffsetAfterDelay(duration));
        }
    }

    #endregion

    #region 私有协程方法

    private IEnumerator DramaticCloseUpCoroutine(float duration)
    {
        // 保存原始设置
        float originalSpeed = GetCurrentSmoothSpeed();
        
        // 快速缩放到特写
        cameraManager.ZoomTo(dramaticZoomInSize);
        cameraManager.SetSmoothSpeed(fastFollowSpeed);
        
        // 添加轻微震动
        cameraManager.ShakeCamera(lightShakeAmount);
        
        // 等待指定时间
        yield return new WaitForSeconds(duration);
        
        // 恢复原始设置
        cameraManager.ResetZoom();
        cameraManager.SetSmoothSpeed(originalSpeed);
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ScanEnvironmentCoroutine(float duration)
    {
        // 保存原始设置
        Vector2 originalOffset = GetCurrentOffset();
        float originalSpeed = GetCurrentSmoothSpeed();
        
        // 放大视野
        cameraManager.ZoomTo(wideZoomOutSize);
        cameraManager.SetSmoothSpeed(slowFollowSpeed);
        
        // 计算扫描步骤
        float stepTime = duration / 4f;
        
        // 向右看
        cameraManager.SetOffset(new Vector2(lookAheadOffset.x, 0));
        yield return new WaitForSeconds(stepTime);
        
        // 向上看
        cameraManager.SetOffset(new Vector2(0, lookUpOffset.y));
        yield return new WaitForSeconds(stepTime);
        
        // 向左看
        cameraManager.SetOffset(new Vector2(-lookAheadOffset.x, 0));
        yield return new WaitForSeconds(stepTime);
        
        // 向下看
        cameraManager.SetOffset(new Vector2(0, lookDownOffset.y));
        yield return new WaitForSeconds(stepTime);
        
        // 恢复原始设置
        cameraManager.ResetZoom();
        cameraManager.SetOffset(originalOffset);
        cameraManager.SetSmoothSpeed(originalSpeed);
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ExplosionEffectCoroutine()
    {
        // 保存原始设置
        float originalZoom = normalZoomSize;
        
        // 添加强烈震动
        cameraManager.ShakeCamera(explosionShakeAmount);
        
        // 短暂缩小视野（模拟冲击波）
        cameraManager.ZoomTo(originalZoom - 1f);
        yield return new WaitForSeconds(0.2f);
        
        // 然后迅速放大（模拟膨胀）
        cameraManager.ZoomTo(originalZoom + 1f);
        yield return new WaitForSeconds(0.3f);
        
        // 恢复原始缩放
        cameraManager.ZoomTo(originalZoom);
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 恢复默认设置
        ResetToDefault();
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ResetOffsetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 恢复原始偏移
        cameraManager.SetOffset(originalOffset);
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ResetZoomAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 恢复默认缩放
        cameraManager.ResetZoom();
        
        currentEffectCoroutine = null;
    }
    
    private IEnumerator ResetSpeedAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 恢复原始速度
        cameraManager.SetSmoothSpeed(originalSmoothSpeed);
        
        currentEffectCoroutine = null;
    }

    #endregion

    #region 辅助方法

    private void StopCurrentEffect()
    {
        if (currentEffectCoroutine != null)
        {
            StopCoroutine(currentEffectCoroutine);
            currentEffectCoroutine = null;
        }
    }
    
    private Vector2 GetCurrentOffset()
    {
        // 这里假设我们没有直接访问CameraManager中的offset字段
        // 实际使用时，您可能需要在CameraManager中添加GetOffset方法
        return originalOffset;
    }
    
    private float GetCurrentSmoothSpeed()
    {
        // 这里假设我们没有直接访问CameraManager中的smoothSpeed字段
        // 实际使用时，您可能需要在CameraManager中添加GetSmoothSpeed方法
        return originalSmoothSpeed;
    }

    #endregion
}