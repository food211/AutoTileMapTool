using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObject : MonoBehaviour, IMechanicAction, ISaveable
{
    // 添加一个唯一ID字段
    [SerializeField] private string uniqueID;

    [System.Serializable]
    public enum RotationType
    {
        SelfRotation,      // 自转
        OrbitRotation      // 环绕旋转
    }

    [Header("旋转设置")]
    [SerializeField] private RotationType rotationType = RotationType.SelfRotation;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;
    [SerializeField] private float rotationSpeed = 90f; // 度/秒
    [SerializeField] private bool useLocalRotation = true;

    [Header("环绕旋转设置")]
    [SerializeField] private Transform orbitCenter;
    [SerializeField] private float orbitRadius = 2f;
    [SerializeField] private float orbitCompletionTime = 2f; // 完成一圈所需的时间

    [Header("旋转限制")]
    [SerializeField] private bool limitRotation = false;
    [SerializeField] private float maxRotationAngle = 90f;
    [SerializeField] private bool pingPongRotation = false;

    private bool isActive = false;
    private Coroutine rotationCoroutine;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float currentAngle = 0f;
    private bool reverseDirection = false;

    public bool IsActive => isActive;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // 如果是环绕旋转但没有指定中心，使用当前位置
        if (rotationType == RotationType.OrbitRotation && orbitCenter == null)
        {
            GameObject centerObj = new GameObject($"{gameObject.name}_OrbitCenter");
            centerObj.transform.position = transform.position;
            orbitCenter = centerObj.transform;

            // 将物体移动到轨道上
            transform.position = orbitCenter.position + Vector3.right * orbitRadius;
        }
    }

    public void Activate()
    {
        if (isActive) return;

        isActive = true;

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }

        rotationCoroutine = StartCoroutine(RotateObjectRoutine());
    }

    public void Deactivate()
    {
        if (!isActive) return;

        isActive = false;

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
            rotationCoroutine = null;
        }
    }

    private IEnumerator RotateObjectRoutine()
    {
        while (isActive)
        {
            if (rotationType == RotationType.SelfRotation)
            {
                // 自转
                PerformSelfRotation();
            }
            else
            {
                // 环绕旋转
                PerformOrbitRotation();
            }

            yield return null;
        }
    }

    private void PerformSelfRotation()
    {
        float rotationAmount = rotationSpeed * Time.deltaTime;

        // 如果有旋转限制
        if (limitRotation)
        {
            if (pingPongRotation)
            {
                // 来回旋转
                if (reverseDirection)
                {
                    currentAngle -= rotationAmount;
                    if (currentAngle <= 0)
                    {
                        currentAngle = 0;
                        reverseDirection = false;
                    }
                }
                else
                {
                    currentAngle += rotationAmount;
                    if (currentAngle >= maxRotationAngle)
                    {
                        currentAngle = maxRotationAngle;
                        reverseDirection = true;
                    }
                }

                // 应用旋转
                Quaternion targetRotation = Quaternion.Euler(rotationAxis * currentAngle);

                if (useLocalRotation)
                {
                    transform.localRotation = targetRotation;
                }
                else
                {
                    transform.rotation = startRotation * targetRotation;
                }
            }
            else
            {
                // 限制最大旋转角度
                currentAngle += rotationAmount;
                if (currentAngle <= maxRotationAngle)
                {
                    Quaternion deltaRotation = Quaternion.Euler(rotationAxis * rotationAmount);

                    if (useLocalRotation)
                    {
                        transform.localRotation *= deltaRotation;
                    }
                    else
                    {
                        transform.rotation *= deltaRotation;
                    }
                }
                else
                {
                    isActive = false;
                }
            }
        }
        else
        {
            // 无限制旋转
            Quaternion deltaRotation = Quaternion.Euler(rotationAxis * rotationAmount);

            if (useLocalRotation)
            {
                transform.localRotation *= deltaRotation;
            }
            else
            {
                transform.rotation *= deltaRotation;
            }
        }
    }

    private void PerformOrbitRotation()
    {
        if (orbitCenter == null) return;

        // 计算当前角度
        float angleSpeed = 360f / orbitCompletionTime;
        float angle = Time.deltaTime * angleSpeed;

        // 如果有旋转限制
        if (limitRotation)
        {
            if (pingPongRotation)
            {
                // 来回旋转
                if (reverseDirection)
                {
                    currentAngle -= angle;
                    if (currentAngle <= 0)
                    {
                        currentAngle = 0;
                        reverseDirection = false;
                    }
                }
                else
                {
                    currentAngle += angle;
                    if (currentAngle >= maxRotationAngle)
                    {
                        currentAngle = maxRotationAngle;
                        reverseDirection = true;
                    }
                }

                // 计算新位置
                Vector3 offset = new Vector3(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad),
                    0
                ) * orbitRadius;

                transform.position = orbitCenter.position + offset;
            }
            else
            {
                // 限制最大旋转角度
                currentAngle += angle;
                if (currentAngle <= maxRotationAngle)
                {
                    transform.RotateAround(
                        orbitCenter.position,
                        rotationAxis,
                        angle
                    );
                }
                else
                {
                    isActive = false;
                }
            }
        }
        else
        {
            // 无限制旋转
            transform.RotateAround(
                orbitCenter.position,
                rotationAxis,
                angle
            );
        }

        // 保持物体朝向
        if (useLocalRotation)
        {
            transform.rotation = startRotation;
        }
    }

    // 在编辑器中可视化旋转路径
    private void OnDrawGizmos()
    {
        if (rotationType == RotationType.OrbitRotation && orbitCenter != null)
        {
            Gizmos.color = Color.cyan;
            DrawCircle(orbitCenter.position, rotationAxis, orbitRadius, 32);
        }
    }

    private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 from = Vector3.Cross(normal, Vector3.up);
        if (from.magnitude < 0.01f)
        {
            from = Vector3.Cross(normal, Vector3.right);
        }
        from = from.normalized * radius;

        Quaternion rotation = Quaternion.AngleAxis(360f / segments, normal);
        Vector3 to = from;

        for (int i = 0; i < segments; i++)
        {
            to = rotation * from;
            Gizmos.DrawLine(center + from, center + to);
            from = to;
        }
    }
    #region ISaveable Implementation
    
    public string GetUniqueID()
    {
        // 如果没有设置ID，自动生成一个
        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = $"RotateObject_{gameObject.name}_{GetInstanceID()}";
        }
        return uniqueID;
    }
    
    public SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "RotateObject";
        
        // 保存当前状态
        data.boolValue = isActive;
        data.floatValue = currentAngle;
        
        // 保存旋转方向
        data.intValue = reverseDirection ? 1 : 0;
        
        // 保存当前旋转
        Quaternion rotation = transform.rotation;
        data.stringValue = $"{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
        
        return data;
    }
    
    public void Load(SaveData data)
    {
        if (data == null || data.objectType != "RotateObject") return;
        
        // 加载当前旋转
        if (!string.IsNullOrEmpty(data.stringValue))
        {
            string[] rotComponents = data.stringValue.Split(',');
            if (rotComponents.Length == 4)
            {
                float x = float.Parse(rotComponents[0]);
                float y = float.Parse(rotComponents[1]);
                float z = float.Parse(rotComponents[2]);
                float w = float.Parse(rotComponents[3]);
                transform.rotation = new Quaternion(x, y, z, w);
            }
        }
        
        // 加载当前角度
        currentAngle = data.floatValue;
        
        // 加载旋转方向
        reverseDirection = data.intValue > 0;
        
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