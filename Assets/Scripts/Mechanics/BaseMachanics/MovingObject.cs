using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingObject : MonoBehaviour, IMechanicAction, ISaveable
{
    // 添加一个唯一ID字段
    [SerializeField] private string uniqueID;
    public bool debugmode = false;

    [System.Serializable]
    public enum MovementType
    {
        Linear,         // 线性移动
        PingPong,       // 来回移动
        Loop,           // 循环路径移动
        OneTime,        // 一次性移动
        Toggle,          // 切换端点移动（新增）
        PlayerControlled 
    }

    [Header("移动设置")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float delayBetweenPoints = 0f;
    [SerializeField] private bool useLocalSpace = false;

    [Header("玩家控制设置")]
    [SerializeField] private float returnDelay = 0.5f; // 返回延迟时间
    [SerializeField] private bool autoReturnWhenDeactivated = true; // 停用时是否自动返回
    private Coroutine returnCoroutine; // 返回协程引用

    [Header("路径点")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private bool returnToStart = false;

    private bool isActive = false;
    private int currentWaypointIndex = 0;
    private Vector3 startPosition;
    private Coroutine movementCoroutine;
    private bool isMovingForward = true; // 用于Toggle模式，标记移动方向

    public bool IsActive => isActive;

    private void Awake()
    {
        startPosition = transform.position;
    }

    public void Activate()
    {
        // 如果已经激活，不需要重复激活
        if (isActive) return;

        // 设置激活状态
        isActive = true;

        // 如果没有指定路径点，则使用起始位置作为第一个点
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("MovingObject: No waypoints specified!");
            return;
        }

        // 如果有正在运行的返回协程，停止它
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        // 开始移动
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        // 对于Toggle模式，每次激活时切换方向
        if (movementType == MovementType.Toggle)
        {
            isMovingForward = !isMovingForward;

            // 设置起始点和目标点
            if (isMovingForward)
            {
                currentWaypointIndex = 0; // 从第一个点开始
            }
            else
            {
                currentWaypointIndex = waypoints.Length - 1; // 从最后一个点开始
            }
        }

        movementCoroutine = StartCoroutine(MoveObject());
    }

public void Deactivate()
{
    // 如果已经停用，不需要重复停用
    if (!isActive)
    {
#if UNITY_EDITOR
        if (debugmode)
        Debug.LogFormat($"MovingObject {gameObject.name} 已经处于停用状态，忽略 Deactivate 调用");
        #endif
        return;
    }

    // 设置停用状态
    isActive = false;

#if UNITY_EDITOR
    if (debugmode)
    Debug.LogFormat($"MovingObject {gameObject.name} 被停用，当前移动类型: {movementType}");
    #endif

    // 停止移动协程
    if (movementCoroutine != null)
    {
        StopCoroutine(movementCoroutine);
        movementCoroutine = null;
#if UNITY_EDITOR
        if (debugmode)
        Debug.LogFormat($"MovingObject {gameObject.name} 停止了移动协程");
        #endif
    }

    // 如果是玩家控制模式且设置了自动返回，则启动返回协程
    if (movementType == MovementType.PlayerControlled && autoReturnWhenDeactivated)
    {
#if UNITY_EDITOR
        if (debugmode)
        Debug.LogFormat($"MovingObject {gameObject.name} 是 PlayerControlled 模式，autoReturnWhenDeactivated={autoReturnWhenDeactivated}，准备返回起点");
        #endif
        
        // 确保没有其他返回协程在运行
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
#if UNITY_EDITOR
            if (debugmode)
            Debug.LogFormat($"MovingObject {gameObject.name} 停止了之前的返回协程");
            #endif
        }

        // 无论当前位置如何，都返回起点
        returnCoroutine = StartCoroutine(ReturnToStartPosition());

#if UNITY_EDITOR
        if (debugmode)
        Debug.LogFormat($"MovingObject {gameObject.name} 开始返回起点，当前位置: {transform.position}, 起点: {startPosition}");
        #endif
    }
    #if UNITY_EDITOR
    else
    {
            if (movementType != MovementType.PlayerControlled)
                if (debugmode)
                {
                    Debug.LogFormat($"MovingObject {gameObject.name} 不是 PlayerControlled 模式，不会自动返回");
                }
                else if (!autoReturnWhenDeactivated)
                if (debugmode)
                    {Debug.LogFormat($"MovingObject {gameObject.name} 的 autoReturnWhenDeactivated 未开启，不会自动返回");}
    }
    #endif
}

    private IEnumerator ReturnToStartPosition()
    {
        // 等待指定的延迟时间
        if (returnDelay > 0)
        {
            yield return new WaitForSeconds(returnDelay);
        }

        // 如果已经被重新激活，取消返回
        if (isActive)
        {
            returnCoroutine = null;
            yield break;
        }

        // 创建一个新的协程来平滑地返回到起始位置
        float returnSpeed = moveSpeed * 1.5f; // 返回速度可以比正常移动速度快一些

#if UNITY_EDITOR
if (debugmode)
        Debug.LogFormat($"平台 {gameObject.name} 正在返回起点，当前位置: {transform.position}, 目标位置: {startPosition}");
#endif

        while (Vector3.Distance(transform.position, startPosition) > 0.01f)
        {
            // 如果被重新激活，立即取消返回
            if (isActive)
            {
#if UNITY_EDITOR
if (debugmode)
                Debug.LogFormat($"平台 {gameObject.name} 返回被中断，因为平台被重新激活");
#endif

                returnCoroutine = null;
                yield break;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                startPosition,
                returnSpeed * Time.deltaTime
            );

            yield return null;
        }

        // 如果没有被重新激活，确保位置精确回到起点
        if (!isActive)
        {
            transform.position = startPosition;
            currentWaypointIndex = 0; // 重置路径点索引

#if UNITY_EDITOR
if (debugmode)
            Debug.LogFormat($"平台 {gameObject.name} 已返回起点");
#endif
        }

        returnCoroutine = null;
    }

    private IEnumerator MoveObject()
    {
        while (isActive)
        {
            Vector3 targetPosition = GetWaypointPosition(currentWaypointIndex);

            // 移动到目标点
            while (Vector3.Distance(GetCurrentPosition(), targetPosition) > 0.01f)
            {
                // 如果在移动过程中被停用，直接退出
                if (!isActive)
                {
                    yield break;
                }

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            // 到达目标点后，更新下一个目标点
            UpdateNextWaypoint();

            // 如果有延迟，则等待
            if (delayBetweenPoints > 0)
            {
                yield return new WaitForSeconds(delayBetweenPoints);
            }

            // 如果是一次性移动，则完成后停止
            if (movementType == MovementType.OneTime && currentWaypointIndex == 0)
            {
                isActive = false;
                yield break;
            }

            // 如果是Toggle模式，当到达终点时自动停用
            if (movementType == MovementType.Toggle)
            {
                // 如果正向移动且到达最后一个点，或反向移动且到达第一个点
                if ((isMovingForward && currentWaypointIndex == 0) ||
                    (!isMovingForward && currentWaypointIndex == waypoints.Length - 1))
                {
                    isActive = false;
                    yield break;
                }
            }

            // 对于PlayerControlled模式，我们不在这里停止，而是让它继续移动
            // 直到玩家离开平台（由Deactivate方法处理）
        }
    }


    private void UpdateNextWaypoint()
    {
        switch (movementType)
        {
            case MovementType.Linear:
                // 原有代码保持不变
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    if (returnToStart)
                    {
                        currentWaypointIndex = 0;
                    }
                    else
                    {
                        currentWaypointIndex = waypoints.Length - 1;
                        isActive = false;
                    }
                }
                break;

            case MovementType.PingPong:
                // 原有代码保持不变
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = waypoints.Length - 2;
                    if (currentWaypointIndex < 0) currentWaypointIndex = 0;

                    // 反转路径点顺序
                    System.Array.Reverse(waypoints);
                }
                break;

            case MovementType.Loop:
                // 原有代码保持不变
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                break;

            case MovementType.OneTime:
                // 原有代码保持不变
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = 0;
                }
                break;

            case MovementType.Toggle:
                // 原有代码保持不变
                if (isMovingForward)
                {
                    // 正向移动：从第一个点到最后一个点
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Length)
                    {
                        // 到达终点，准备下次从终点返回
                        currentWaypointIndex = waypoints.Length - 1;
                    }
                }
                else
                {
                    // 反向移动：从最后一个点到第一个点
                    currentWaypointIndex--;
                    if (currentWaypointIndex < 0)
                    {
                        // 到达起点，准备下次从起点出发
                        currentWaypointIndex = 0;
                    }
                }
                break;

            case MovementType.PlayerControlled:
                // 玩家控制模式：只向前移动到B点，不自动返回
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    // 到达最后一个点后，停留在最后一个点
                    currentWaypointIndex = waypoints.Length - 1;
                }
                break;
        }
    }

    private Vector3 GetWaypointPosition(int index)
    {
        if (waypoints[index] != null)
        {
            return waypoints[index].position;
        }
        else
        {
            Debug.LogWarning($"MovingObject: Waypoint at index {index} is null!");
            return transform.position;
        }
    }

    private Vector3 GetCurrentPosition()
    {
        return useLocalSpace ? transform.localPosition : transform.position;
    }

    // 获取当前移动方向（用于Toggle模式）
    public bool IsMovingForward()
    {
        return isMovingForward;
    }

    // 设置初始状态（对于Toggle模式很有用）
    public void SetInitialState(bool startAtFirstPoint)
    {
        if (movementType == MovementType.Toggle)
        {
            // 设置初始位置和方向
            isMovingForward = !startAtFirstPoint; // 下次激活时会切换

            // 直接设置位置到对应端点
            if (startAtFirstPoint && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
                currentWaypointIndex = 0;
            }
            else if (!startAtFirstPoint && waypoints.Length > 0 && waypoints[waypoints.Length - 1] != null)
            {
                transform.position = waypoints[waypoints.Length - 1].position;
                currentWaypointIndex = waypoints.Length - 1;
            }
        }
    }

    // 用于在编辑器中可视化路径
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Vector3 position = waypoints[i].position;

            // 绘制路径点
            Gizmos.DrawSphere(position, 0.2f);

            // 绘制路径线
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(position, waypoints[i + 1].position);
            }

            // 如果是循环或返回起点，则连接最后一个点和第一个点
            if ((movementType == MovementType.Loop || returnToStart) &&
                i == waypoints.Length - 1 && waypoints[0] != null)
            {
                Gizmos.DrawLine(position, waypoints[0].position);
            }
        }

        // 为Toggle模式添加特殊标记
        if (movementType == MovementType.Toggle && waypoints.Length >= 2)
        {
            // 标记起点和终点
            if (waypoints[0] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(waypoints[0].position, 0.25f);
            }

            if (waypoints[waypoints.Length - 1] != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(waypoints[waypoints.Length - 1].position, 0.25f);
            }
        }

        // 为PlayerControlled模式添加特殊标记
        if (movementType == MovementType.PlayerControlled)
        {
            // 标记起始位置（玩家离开后返回的位置）
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(startPosition, 0.25f);

            // 绘制从起始位置到第一个路径点的虚线，表示返回路径
            if (waypoints.Length > 0 && waypoints[0] != null)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.5f);
                DrawDottedLine(startPosition, waypoints[0].position, 0.2f);
            }
        }
    }
    private void DrawDottedLine(Vector3 start, Vector3 end, float dashSize)
    {
#if UNITY_EDITOR
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);

        int dashCount = Mathf.FloorToInt(distance / dashSize);

        for (int i = 0; i < dashCount; i += 2)
        {
            float t1 = i * dashSize / distance;
            float t2 = Mathf.Min(1.0f, (i + 1) * dashSize / distance);

            Gizmos.DrawLine(Vector3.Lerp(start, end, t1), Vector3.Lerp(start, end, t2));
        }
#endif
    }
    #region ISaveable Implementation
    
    public string GetUniqueID()
    {
        // 如果没有设置ID，自动生成一个
        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = $"MovingObject_{gameObject.name}_{GetInstanceID()}";
        }
        return uniqueID;
    }
    
    public SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "MovingObject";
        
        // 保存当前状态
        data.boolValue = isActive;
        data.intValue = currentWaypointIndex;
        
        // 保存当前位置
        Vector3 position = transform.position;
        data.stringValue = $"{position.x},{position.y},{position.z}";
        
        // 保存移动方向（用于Toggle模式）
        data.floatValue = isMovingForward ? 1 : 0;
        
        return data;
    }
    
    public void Load(SaveData data)
    {
        if (data == null || data.objectType != "MovingObject") return;
        
        // 加载当前位置
        if (!string.IsNullOrEmpty(data.stringValue))
        {
            string[] posComponents = data.stringValue.Split(',');
            if (posComponents.Length == 3)
            {
                float x = float.Parse(posComponents[0]);
                float y = float.Parse(posComponents[1]);
                float z = float.Parse(posComponents[2]);
                transform.position = new Vector3(x, y, z);
            }
        }
        
        // 加载当前路径点索引
        currentWaypointIndex = data.intValue;
        
        // 加载移动方向
        isMovingForward = data.floatValue > 0;
        
        // 加载活动状态
        if (data.boolValue)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }
    
    #endregion
}