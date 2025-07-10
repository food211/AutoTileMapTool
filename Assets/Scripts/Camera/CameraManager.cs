using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.Mathematics;

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
    [SerializeField] private float noiseSpeed = 10f; // 噪声速度

    [Header("下落偏移调整")]
    [SerializeField] private bool enableFallingOffsetAdjustment = true; // 是否启用下落偏移调整
    [SerializeField] private float maxPositiveYOffset;
    [SerializeField] private float maxNegativeYOffset;
    [SerializeField] private float offsetTransitionSpeed = 0.25f; // 偏移过渡速度
    [SerializeField] private float fallingThreshold = -0.5f; // 下落速度阈值，低于此值视为下落

    private GameObject topLetterbox;
    private GameObject bottomLetterbox;
    private float letterboxTargetHeight = 0.1f; // 默认黑边高度为屏幕的10%
    private Coroutine storyModeCoroutine;

    private Vector3 lastTargetPosition; // 上一帧目标位置
    private float currentYOffset; // 当前Y轴偏移量
    private bool wasTargetFalling; // 上一帧目标是否在下落
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
            Debug.LogWarning("摄像机没有设置跟随目标!请在Inspector中设置Target。");
        }
        if (target != null)
        {
            lastTargetPosition = target.position;
        }
        currentYOffset = offset.y;
        maxPositiveYOffset = math.abs(offset.y);
        maxNegativeYOffset = -math.abs(offset.y);

        // 保存初始Z位置和旋转
        initialZ = transform.position.z;
        initialRotation = transform.rotation;

        // 获取相机组件
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("CameraManager脚本必须挂载在有Camera组件的GameObject上!");
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
        GameEvents.OnStoryModeChanged += ShowLetterboxes;

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
        GameEvents.OnStoryModeChanged -= ShowLetterboxes;


        // 销毁虚拟目标
        if (pointOfInterestTarget != null)
        {
            Destroy(pointOfInterestTarget.gameObject);
        }
        // 销毁黑边
        if (topLetterbox != null)
            Destroy(topLetterbox);
        if (bottomLetterbox != null)
            Destroy(bottomLetterbox);
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

        // 计算目标垂直速度
        Vector3 targetVelocity = Vector3.zero;
        if (Time.deltaTime > 0)
        {
            targetVelocity = (currentTarget.position - lastTargetPosition) / Time.deltaTime;
        }
        lastTargetPosition = currentTarget.position;

        // 根据垂直速度调整Y轴偏移量
        if (enableFallingOffsetAdjustment)
        {
            bool isTargetFalling = targetVelocity.y < fallingThreshold;

            // 计算目标偏移量
            float targetYOffset = offset.y; // 默认使用原始偏移量

            if (isTargetFalling)
            {
                // 当目标下落时，向负方向过渡
                targetYOffset = maxNegativeYOffset;
            }
            else if (wasTargetFalling || currentYOffset < offset.y)
            {
                // 当目标停止下落或正在恢复时，向正方向过渡
                targetYOffset = maxPositiveYOffset;

                // 如果已经接近原始偏移量，则恢复到原始偏移量
                if (Mathf.Abs(currentYOffset - offset.y) < 0.1f)
                {
                    targetYOffset = offset.y;
                }
            }

            // 平滑过渡到目标偏移量
            currentYOffset = Mathf.Lerp(currentYOffset, targetYOffset, offsetTransitionSpeed * Time.deltaTime);

            // 更新下落状态
            wasTargetFalling = isTargetFalling;
        }
        else
        {
            // 如果不启用下落偏移调整，使用原始偏移量
            currentYOffset = offset.y;
        }

        // 计算目标位置（考虑偏移量），但只考虑X和Y
        Vector3 targetPosition = new Vector3(
            currentTarget.position.x + offset.x,
            currentTarget.position.y + currentYOffset, // 使用动态Y轴偏移量
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
            float offsetX = maxShakePosition * shake * GetPerlinNoise(Time.time, 0f, noiseSpeed);
            float offsetY = maxShakePosition * shake * GetPerlinNoise(Time.time, 1f, noiseSpeed);
            shakeOffset = new Vector3(offsetX, offsetY, 0);
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        // 应用旋转震动
        if (enableRotationShake)
        {
            float rotZ = maxShakeRotation * shake * GetPerlinNoise(Time.time, 2f, noiseSpeed);
            transform.rotation = initialRotation * Quaternion.Euler(0, 0, rotZ);
        }
        else
        {
            transform.rotation = initialRotation;
        }
    }

    private float GetPerlinNoise(float time, float seed, float speed = 1.0f)
    {
        // 使用时间和种子生成柏林噪声
        // 将0-1范围的噪声值转换为-1到1的范围
        return (Mathf.PerlinNoise(time * speed, seed) * 2.0f - 1.0f);
    }

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

    #region 公开方法：设置偏移量
    public void SetOffset(Vector2 newOffset)
    {
        offset = newOffset;
    }

    public void SetTargetHeight(float height)
    {
        letterboxTargetHeight = height;
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
#endregion
    #region 震动相关方法

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
    #endregion
    #region StoryMode

    private void CreateLetterboxes()
    {
        // 确保有Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            // 如果场景中没有Canvas，创建一个
            GameObject canvasObj = new GameObject("LetterboxCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 确保在最上层
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // 使用对象池获取或创建黑边
        if (topLetterbox == null)
        {
            // 尝试从对象池获取黑边
            GameObject letterboxPrefab = Resources.Load<GameObject>("UI/Letterbox");
            if (letterboxPrefab != null)
            {
                // 如果有预制体，从对象池获取
                if (ObjectPoolClass.ObjectPool.Instance != null)
                {
                    // 顶部黑边
                    GameObject topObj = ObjectPoolClass.ObjectPool.Instance.GetObject(
                        letterboxPrefab,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    topObj.transform.SetParent(canvas.transform, false);
                    var topRect = topObj.GetComponent<RectTransform>();
                    topRect.anchorMin = new Vector2(0, 1);
                    topRect.anchorMax = new Vector2(1, 1);
                    topRect.pivot = new Vector2(0.5f, 1);
                    topRect.sizeDelta = new Vector2(0, 0);
                    topLetterbox = topObj;

                    // 底部黑边
                    GameObject bottomObj = ObjectPoolClass.ObjectPool.Instance.GetObject(
                        letterboxPrefab,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    bottomObj.transform.SetParent(canvas.transform, false);
                    var bottomRect = bottomObj.GetComponent<RectTransform>();
                    bottomRect.anchorMin = new Vector2(0, 0);
                    bottomRect.anchorMax = new Vector2(1, 0);
                    bottomRect.pivot = new Vector2(0.5f, 0);
                    bottomRect.sizeDelta = new Vector2(0, 0);
                    bottomLetterbox = bottomObj;
                }
            }

            // 如果对象池获取失败，创建新对象
            if (topLetterbox == null)
            {
                // 创建顶部黑边
                GameObject topObj = new GameObject("TopLetterbox");
                topObj.transform.SetParent(canvas.transform, false);
                var topImage = topObj.AddComponent<UnityEngine.UI.Image>();
                topImage.color = Color.black;
                var topRect = topObj.GetComponent<RectTransform>();
                topRect.anchorMin = new Vector2(0, 1);
                topRect.anchorMax = new Vector2(1, 1);
                topRect.pivot = new Vector2(0.5f, 1);
                topRect.sizeDelta = new Vector2(0, 0);
                topLetterbox = topObj;
            }
        }

        if (bottomLetterbox == null)
        {
            // 如果顶部创建成功但底部为空，说明没有使用对象池
            // 创建底部黑边
            GameObject bottomObj = new GameObject("BottomLetterbox");
            bottomObj.transform.SetParent(canvas.transform, false);
            var bottomImage = bottomObj.AddComponent<UnityEngine.UI.Image>();
            bottomImage.color = Color.black;
            var bottomRect = bottomObj.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0);
            bottomRect.pivot = new Vector2(0.5f, 0);
            bottomRect.sizeDelta = new Vector2(0, 0);
            bottomLetterbox = bottomObj;
        }

        // 初始状态为隐藏
        topLetterbox.SetActive(false);
        bottomLetterbox.SetActive(false);
    }

    // 显示黑边
    private void ShowLetterboxes(bool show, float duration = 0.5f)
    {
        if (show)
        {
            // 显示黑边
            CreateLetterboxes(); // 确保黑边对象已创建

            // 启动动画协程
            if (storyModeCoroutine != null)
                StopCoroutine(storyModeCoroutine);

            storyModeCoroutine = StartCoroutine(AnimateLetterboxes(true, letterboxTargetHeight, duration));
        }
        else
        {
            // 隐藏黑边
            if (storyModeCoroutine != null)
                StopCoroutine(storyModeCoroutine);

            storyModeCoroutine = StartCoroutine(AnimateLetterboxes(false, letterboxTargetHeight, duration));
        }
    }

    // 隐藏黑边
    private void HideLetterboxes()
    {
        if (topLetterbox != null)
        {
            // 检查是否可以使用对象池
            if (ObjectPoolClass.ObjectPool.Instance != null)
            {
                // 返回对象池
                ObjectPoolClass.ObjectPool.Instance.ReturnObject(topLetterbox);
            }
            else
            {
                // 如果没有对象池，只是禁用
                topLetterbox.SetActive(false);
            }
            topLetterbox = null;
        }

        if (bottomLetterbox != null)
        {
            // 检查是否可以使用对象池
            if (ObjectPoolClass.ObjectPool.Instance != null)
            {
                // 返回对象池
                ObjectPoolClass.ObjectPool.Instance.ReturnObject(bottomLetterbox);
            }
            else
            {
                // 如果没有对象池，只是禁用
                bottomLetterbox.SetActive(false);
            }
            bottomLetterbox = null;
        }
    }

    private IEnumerator AnimateLetterboxes(bool show, float targetHeight, float duration)
    {
        // 确保黑边已创建
        if (topLetterbox == null || bottomLetterbox == null)
        {
            if (show) // 只在显示时创建
                CreateLetterboxes();
            else
                yield break; // 如果是隐藏但黑边不存在，直接返回
        }

        // 获取RectTransform组件
        RectTransform topRect = topLetterbox.GetComponent<RectTransform>();
        RectTransform bottomRect = bottomLetterbox.GetComponent<RectTransform>();

        // 计算目标高度（屏幕高度的百分比）
        float screenHeight = Screen.height;
        float targetLetterboxHeight = screenHeight * targetHeight;

        // 设置初始状态
        if (show)
        {
            // 显示黑边对象
            topLetterbox.SetActive(true);
            bottomLetterbox.SetActive(true);

            // 初始高度为0
            topRect.sizeDelta = new Vector2(0, 0);
            bottomRect.sizeDelta = new Vector2(0, 0);
        }

        // 使用传入的持续时间
        float elapsed = 0f;

        // 记录初始高度
        float initialTopHeight = topRect.sizeDelta.y;
        float initialBottomHeight = bottomRect.sizeDelta.y;

        // 执行动画
        while (elapsed < duration)
        {
            // 计算插值因子（使用平滑过渡）
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);

            if (show)
            {
                // 从0增加到目标高度
                float currentHeight = Mathf.Lerp(initialTopHeight, targetLetterboxHeight, t);
                topRect.sizeDelta = new Vector2(0, currentHeight);
                bottomRect.sizeDelta = new Vector2(0, currentHeight);
            }
            else
            {
                // 从当前高度减少到0
                float currentHeight = Mathf.Lerp(initialTopHeight, 0, t);
                topRect.sizeDelta = new Vector2(0, currentHeight);
                bottomRect.sizeDelta = new Vector2(0, currentHeight);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 设置最终状态
        if (show)
        {
            // 确保达到精确的目标高度
            topRect.sizeDelta = new Vector2(0, targetLetterboxHeight);
            bottomRect.sizeDelta = new Vector2(0, targetLetterboxHeight);
        }
        else
        {
            // 完全隐藏黑边并清理
            HideLetterboxes();
        }

        storyModeCoroutine = null;
    }

    #endregion
}