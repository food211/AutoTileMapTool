using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Transform target; // 跟随的目标Transform
    [SerializeField] private float smoothSpeed = 0.1f; // 平滑速度，默认为0.1
    [SerializeField] private Vector2 offset = Vector2.zero; // 与目标的偏移量(仅X和Y)
    
    // 在Inspector中可以设置是否在X或Y轴上跟随
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;

    
    // 缩放相关参数
    [SerializeField] private float defaultSize = 5f; // 默认正交相机大小
    [SerializeField] private float zoomInSize = 3f; // 放大时的相机大小
    [SerializeField] private float zoomOutSize = 8f; // 缩小时的相机大小
    [SerializeField] private float zoomSmoothSpeed = 0.1f; // 缩放平滑速度
    
    // 震动相关参数
    [Header("屏幕震动设置")]
    [SerializeField] private bool enableShake = true; // 是否启用震动
    [SerializeField] private bool enablePositionShake = true; // 是否启用位置震动
    [SerializeField] private bool enableRotationShake = true; // 是否启用旋转震动
    [SerializeField] private float maxShakePosition = 10f; // 最大震动位移
    [SerializeField] private float maxShakeRotation = 10f; // 最大震动旋转角度
    [SerializeField] private float traumaDecayRate = 0.25f; // trauma衰减速率
    [SerializeField] private float traumaPower = 2f; // trauma强度指数(2=平方，3=立方)
    
    // 保存摄像机的初始Z位置和旋转
    private float initialZ;
    private Quaternion initialRotation;
    private Camera cam;
    private float targetSize; // 目标缩放大小
    private float trauma = 0f; // 当前trauma值，范围0-1
    private Vector3 shakeOffset = Vector3.zero; // 震动位移偏移量
    private Vector3 basePosition; // 基础位置（不包含震动）
    private bool allowNewShakes = true; // 是否允许新的震动
    
    // 兴趣点相关变量
    private bool followingPointsOfInterest = false; // 是否正在跟随兴趣点
    private Transform originalTarget; // 原始跟随目标
    private Transform pointOfInterestTarget; // 兴趣点虚拟目标
    private Coroutine pointsOfInterestCoroutine; // 兴趣点协程引用
    private string currentSequenceId; // 当前正在执行的序列ID

    void Start()
    {
        // 确保有目标可以跟随
        if (target == null)
        {
            Debug.LogWarning("摄像机没有设置跟随目标！请在Inspector中设置Target。");
        }
        
        // 保存初始Z位置和旋转
        initialZ = transform.position.z;
        initialRotation = transform.rotation;
        
        // 获取相机组件
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("CameraManager脚本必须挂载在有Camera组件的GameObject上！");
            return;
        }
        
        // 设置初始目标大小
        targetSize = cam.orthographicSize;
        
        // 创建兴趣点虚拟目标
        GameObject targetObj = new GameObject("CameraPointOfInterestTarget");
        pointOfInterestTarget = targetObj.transform;
        targetObj.transform.SetParent(transform.parent);
        
        // 订阅缩放事件
        GameEvents.OnCameraZoomIn += ZoomIn;
        GameEvents.OnCameraZoomOut += ZoomOut;
        GameEvents.OnCameraZoomReset += ResetZoom;
        GameEvents.OnCameraZoomTo += ZoomTo;
        
        // 订阅震动事件
        GameEvents.OnCameraShake += ShakeCamera;
        
        // 订阅兴趣点事件
        GameEvents.OnFollowPointsOfInterest += FollowPointsOfInterest;
        GameEvents.OnStopFollowingPointsOfInterest += StopFollowingPointsOfInterest;
    }
    
    void OnDestroy()
    {
        // 取消订阅事件，防止内存泄漏
        GameEvents.OnCameraZoomIn -= ZoomIn;
        GameEvents.OnCameraZoomOut -= ZoomOut;
        GameEvents.OnCameraZoomReset -= ResetZoom;
        GameEvents.OnCameraZoomTo -= ZoomTo;
        GameEvents.OnCameraShake -= ShakeCamera;
        GameEvents.OnFollowPointsOfInterest -= FollowPointsOfInterest;
        GameEvents.OnStopFollowingPointsOfInterest -= StopFollowingPointsOfInterest;
        
        // 销毁虚拟目标
        if (pointOfInterestTarget != null)
        {
            Destroy(pointOfInterestTarget.gameObject);
        }
    }

    // 使用LateUpdate确保在所有Update完成后再移动摄像机
    void FixedUpdate()
    {
        // 处理位置跟随
        FollowTarget();
        
        // 处理缩放平滑过渡
        SmoothZoom();
        
        // 处理屏幕震动
        UpdateShake();
    }
    
    private void FollowTarget()
    {
        // 获取当前要跟随的目标
        Transform currentTarget = target;
        
        // 如果没有目标，直接返回
        if (currentTarget == null)
            return;

        // 获取当前位置
        Vector3 currentPosition = transform.position;
        Vector3 newPosition = currentPosition;
        
        // 计算目标位置（考虑偏移量），但只考虑X和Y
        Vector3 targetPosition = new Vector3(
            currentTarget.position.x + offset.x,
            currentTarget.position.y + offset.y,
            initialZ  // 保持原始Z值
        );

        // 根据公式: x += (target-x)*0.1 计算新位置
        if (followX)
            newPosition.x += (targetPosition.x - currentPosition.x) * smoothSpeed;
        if (followY)
            newPosition.y += (targetPosition.y - currentPosition.y) * smoothSpeed;
        
        // Z轴保持不变
        newPosition.z = initialZ;

        // 保存基础位置（不包含震动）
        basePosition = newPosition;
        
        // 如果有震动效果，添加震动偏移
        if (trauma > 0 && enablePositionShake)
        {
            newPosition += shakeOffset;
        }

        // 更新摄像机位置
        transform.position = newPosition;
    }
    
    private void SmoothZoom()
    {
        if (cam == null || Mathf.Approximately(cam.orthographicSize, targetSize))
            return;
            
        // 应用平滑缩放
        cam.orthographicSize += (targetSize - cam.orthographicSize) * zoomSmoothSpeed;
        
        // 如果非常接近目标值，直接设置为目标值
        if (Mathf.Abs(cam.orthographicSize - targetSize) < 0.01f)
        {
            cam.orthographicSize = targetSize;
        }
    }
    
    private void UpdateShake()
    {
        // 如果trauma为0，重置震动并返回
        if (trauma <= 0)
        {
            // 重置震动
            shakeOffset = Vector3.zero;
            transform.rotation = initialRotation;
            trauma = 0;
            return;
        }
        
        // 随时间衰减trauma值
        trauma = Mathf.Clamp01(trauma - traumaDecayRate * Time.deltaTime);
        
        // 计算当前震动强度（使用trauma的平方或立方来使震动更加动态）
        float shake = Mathf.Pow(trauma, traumaPower);
        
        // 使用柏林噪声生成随机但平滑的震动
        if (enablePositionShake)
        {
            float offsetX = maxShakePosition * shake * (Mathf.PerlinNoise(Time.time * 10, 0) * 2 - 1);
            float offsetY = maxShakePosition * shake * (Mathf.PerlinNoise(0, Time.time * 10) * 2 - 1);
            shakeOffset = new Vector3(offsetX, offsetY, 0);
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
        
        // 应用旋转震动
        if (enableRotationShake)
        {
            float rotZ = maxShakeRotation * shake * (Mathf.PerlinNoise(Time.time * 10, 10) * 2 - 1);
            transform.rotation = initialRotation * Quaternion.Euler(0, 0, rotZ);
        }
        else
        {
            transform.rotation = initialRotation;
        }
    }

    // 兴趣点相关方法
    
    // 跟随一系列兴趣点
    public void FollowPointsOfInterest(List<PointOfInterest> points, string sequenceId)
    {
        // 如果已经在跟随兴趣点，先停止当前的跟随
        if (followingPointsOfInterest)
        {
            StopFollowingPointsOfInterest();
        }
        
        // 保存原始目标和序列ID
        originalTarget = target;
        currentSequenceId = sequenceId;
        
        // 开始跟随兴趣点
        followingPointsOfInterest = true;
        
        // 启动协程
        pointsOfInterestCoroutine = StartCoroutine(FollowPointsOfInterestCoroutine(points));
    }
    
    // 停止跟随兴趣点
    public void StopFollowingPointsOfInterest()
    {
        if (!followingPointsOfInterest)
            return;
            
        // 停止协程
        if (pointsOfInterestCoroutine != null)
        {
            StopCoroutine(pointsOfInterestCoroutine);
            pointsOfInterestCoroutine = null;
        }
        
        // 恢复原始目标
        target = originalTarget;
        
        // 重置状态
        followingPointsOfInterest = false;
        
        // 不触发完成事件，因为是被中断的
    }
    
    // 跟随兴趣点的协程 - 简化版本，复用FollowTarget方法
    private IEnumerator FollowPointsOfInterestCoroutine(List<PointOfInterest> points)
    {
        // 遍历所有兴趣点
        for (int i = 0; i < points.Count; i++)
        {
            PointOfInterest point = points[i];
            
            // 设置虚拟目标的位置
            Vector3 targetPosition;
            
            // 如果使用目标对象，则使用目标对象的位置
            if (point.useTargetObject && point.targetObject != null)
            {
                // 将虚拟目标移动到目标对象位置
                pointOfInterestTarget.position = point.targetObject.transform.position;
                
                // 如果启用了动态跟踪，则直接将相机目标设为目标对象
                if (point.dynamicTracking)
                {
                    // 设置为跟随目标对象
                    target = point.targetObject.transform;
                    
                    // 等待指定的停留时间
                    yield return new WaitForSeconds(point.stayTime);
                    
                    // 继续下一个点
                    continue;
                }
            }
            else
            {
                // 使用兴趣点的位置
                // 如果是局部坐标，需要转换为世界坐标
                if (transform.parent != null)
                {
                    targetPosition = transform.parent.TransformPoint(new Vector3(point.position.x, point.position.y, 0));
                }
                else
                {
                    targetPosition = new Vector3(point.position.x, point.position.y, 0);
                }
                
                // 设置虚拟目标位置
                pointOfInterestTarget.position = targetPosition;
            }
            
            // 将相机目标设置为虚拟目标
            target = pointOfInterestTarget;
            
            // 等待指定的停留时间
            yield return new WaitForSeconds(point.stayTime);
        }
        
        // 完成所有兴趣点后，恢复原始目标
        target = originalTarget;
        followingPointsOfInterest = false;
        
        // 触发序列完成事件
        GameEvents.TriggerPointsOfInterestSequenceCompleted(currentSequenceId);
    }

    // 公开方法：设置跟随目标
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // 公开方法：设置平滑速度
    public void SetSmoothSpeed(float speed)
    {
        smoothSpeed = speed;
    }

    public float GetSmoothSpeed()
    {
        return smoothSpeed;
    }

    // 公开方法：设置偏移量
    public void SetOffset(Vector2 newOffset)
    {
        offset = newOffset;
    }
    
    // 缩放相关方法
    
    // 放大相机
    public void ZoomIn()
    {
        targetSize = zoomInSize;
    }
    
    // 缩小相机
    public void ZoomOut()
    {
        targetSize = zoomOutSize;
    }
    
    // 重置相机缩放到默认值
    public void ResetZoom()
    {
        targetSize = defaultSize;
    }
    
    // 缩放到指定大小
    public void ZoomTo(float size)
    {
        targetSize = size;
    }
    
    // 震动相关方法
    
    // 添加屏幕震动
    public void ShakeCamera(float traumaAmount)
    {
        // 如果不允许新的震动，直接返回
        if (!allowNewShakes)
            return;
            
        // 累加trauma值，但不超过1
        trauma = Mathf.Clamp01(trauma + traumaAmount);
    }
    
    // 启用/禁用震动
    public void SetShakeEnabled(bool enabled)
    {
        enableShake = enabled;
        allowNewShakes = enabled;
        
        // 注意：这里不再立即重置震动，而是让当前震动自然结束
        // 只是禁止新的震动被添加
    }
    
    // 立即停止所有震动（用于需要立即停止的情况）
    public void StopAllShakesImmediately()
    {
        trauma = 0;
        shakeOffset = Vector3.zero;
        transform.rotation = initialRotation;
    }
}