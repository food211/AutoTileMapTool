using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class OldRope : MonoBehaviour
{
    [Header("Rope")]
    [SerializeField] private int _numOfRopeSegments = 50;
    [SerializeField] private float _ropeSegmentsLength = 0.225f;

    [Header("Physics")]
    [SerializeField] private Vector2 _gravityForce = new Vector2(0, -2f);
    [SerializeField] private float _dampingFactor = 0.98f;//optinal
    [SerializeField] private LayerMask _collisionMask;
    [SerializeField] private float _collisionMaskRadius = 0.1f;
    // [SerializeField] private float _frictionCoefficient = 0.1f; // 新增摩擦系数

    [Header("Constraints")]
    [SerializeField] private int _numOfRopeConstraintRuns = 50;
    [Header("Optimizations")]
    [SerializeField] private int _collisionSegmentInterval = 2;
    [SerializeField] private int _previousNumOfRopeSegments; // 用于记录上一次的段数

    private LineRenderer _lineRenderer;
    private List<RopeSegment> _ropeSegments = new List<RopeSegment>();
    private Vector3 _ropeStartpoint;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = _numOfRopeSegments;

        _ropeStartpoint = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        for (int i = 0; i < _numOfRopeSegments; i++)
        {
            _ropeSegments.Add(new RopeSegment(_ropeStartpoint));
            _ropeStartpoint.y -= _ropeSegmentsLength;
        }
    }

    private void Update()
    {
        // 如果段数发生变化，更新绳子段
        if (_numOfRopeSegments != _previousNumOfRopeSegments)
        {
            UpdateRopeSegments();
            _previousNumOfRopeSegments = _numOfRopeSegments;
        }

        DrawRope();
    }
    
    private void DrawRope()
    {
        Vector3[] ropePositions = new Vector3[_numOfRopeSegments];
        for (int i = 0; i < _ropeSegments.Count; i++)
        {
            ropePositions[i] = _ropeSegments[i].CurrentPosition;
        }
        _lineRenderer.SetPositions(ropePositions);
    }

    private void FixedUpdate()
    {
        Simulate();

        for (int i = 0; i < _numOfRopeConstraintRuns; i++)
        {
            ApplyConstraints();
        }
    }

//绳子的物理模拟
    private void Simulate()
    {
        for (int i = 0; i < _ropeSegments.Count; i++)
        {
            RopeSegment segment = _ropeSegments[i];
            Vector2 velocity = (segment.CurrentPosition - segment.OldPosition) * _dampingFactor;

            segment.OldPosition = segment.CurrentPosition;
            segment.CurrentPosition += velocity;
            segment.CurrentPosition += _gravityForce * Time.fixedDeltaTime;
            _ropeSegments[i] = segment;
        }
    }

private void ApplyConstraints()
{
    // 保持第一个点跟随鼠标位置
    RopeSegment firstSegment = _ropeSegments[0];
    Vector3 mousePosition = Mouse.current.position.ReadValue();
    mousePosition.z = Mathf.Abs(Camera.main.transform.position.z); // 设置 Z 坐标为相机距离
    Vector3 targetPosition = Camera.main.ScreenToWorldPoint(mousePosition);

    firstSegment.CurrentPosition = targetPosition;
    _ropeSegments[0] = firstSegment;

    // 处理其余绳子段的约束
    for (int i = 0; i < _numOfRopeSegments - 1; i++)
    {
        RopeSegment currentSeg = _ropeSegments[i];
        RopeSegment nextSeg = _ropeSegments[i + 1];

        float dist = (currentSeg.CurrentPosition - nextSeg.CurrentPosition).magnitude;
        float difference = (dist - _ropeSegmentsLength);

        Vector2 changeDir = (currentSeg.CurrentPosition - nextSeg.CurrentPosition).normalized;
        Vector2 changeVector = changeDir * difference;
        if (i != 0)
        {
            currentSeg.CurrentPosition -= changeVector * 0.5f;
            nextSeg.CurrentPosition += changeVector * 0.5f;
        }
        else
        {
            nextSeg.CurrentPosition += changeVector;
        }

        // 根据 _collisionSegmentInterval 调节碰撞检测频率
        if (i % _collisionSegmentInterval == 0)
        {
            HandleSegmentCollision(ref currentSeg);
            HandleSegmentCollision(ref nextSeg);
        }


        _ropeSegments[i] = currentSeg;
        _ropeSegments[i + 1] = nextSeg;
    }
}

private void HandleSegmentCollision(ref RopeSegment segment)
{
    Collider2D[] colliders = Physics2D.OverlapCircleAll(segment.CurrentPosition, _collisionMaskRadius, _collisionMask);
    foreach (Collider2D collider in colliders)
    {
        Vector2 closestPoint = collider.ClosestPoint(segment.CurrentPosition);
        float distance = Vector2.Distance(segment.CurrentPosition, closestPoint);

        if (distance < _collisionMaskRadius)
        {
            Vector2 normal = (segment.CurrentPosition - closestPoint).normalized;
            if (normal == Vector2.zero)
            {
                normal = (segment.CurrentPosition - (Vector2)collider.transform.position).normalized;
            }

            float depth = _collisionMaskRadius - distance;

            segment.CurrentPosition += normal * depth;
        }
    }
}

    private void UpdateRopeSegments()
    {
        // 如果新的段数大于当前段数，添加新的 RopeSegment
        while (_ropeSegments.Count < _numOfRopeSegments)
        {
            Vector3 lastSegmentPosition = _ropeSegments[_ropeSegments.Count - 1].CurrentPosition;
            _ropeSegments.Add(new RopeSegment(lastSegmentPosition - new Vector3(0, _ropeSegmentsLength, 0)));
        }

        // 如果新的段数小于当前段数，移除多余的 RopeSegment
        while (_ropeSegments.Count > _numOfRopeSegments)
        {
            _ropeSegments.RemoveAt(_ropeSegments.Count - 1);
        }

        // 更新 LineRenderer 的点数
        _lineRenderer.positionCount = _numOfRopeSegments;
    }
    

    public struct RopeSegment
    {
        public Vector2 CurrentPosition;
        public Vector2 OldPosition;
        public RopeSegment(Vector2 pos)
        {
            CurrentPosition = pos;
            OldPosition = pos;
        }
    }
}
