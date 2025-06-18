using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingObject : MonoBehaviour, IMechanicAction
{
    [System.Serializable]
    public enum MovementType
    {
        Linear,         // 线性移动
        PingPong,       // 来回移动
        Loop,           // 循环路径移动
        OneTime,        // 一次性移动
        Toggle          // 切换端点移动（新增）
    }
    
    [Header("移动设置")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float delayBetweenPoints = 0f;
    [SerializeField] private bool useLocalSpace = false;
    
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
        if (isActive) return;
        
        isActive = true;
        
        // 如果没有指定路径点，则使用起始位置作为第一个点
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("MovingObject: No waypoints specified!");
            return;
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
        if (!isActive) return;
        
        isActive = false;
        
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
    }
    
    private IEnumerator MoveObject()
    {
        while (isActive)
        {
            Vector3 targetPosition = GetWaypointPosition(currentWaypointIndex);
            
            // 移动到目标点
            while (Vector3.Distance(GetCurrentPosition(), targetPosition) > 0.01f)
            {
                if (!isActive) yield break;
                
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
        }
    }
    
    private void UpdateNextWaypoint()
    {
        switch (movementType)
        {
            case MovementType.Linear:
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
                // 实现来回移动的逻辑
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
                // 循环移动
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                break;
                
            case MovementType.OneTime:
                // 一次性移动
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = 0;
                }
                break;
                
            case MovementType.Toggle:
                // Toggle模式：在两个端点之间切换
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
    }
}