using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class RopeVerlet : MonoBehaviour
{
    [Header("Rope")]
    [SerializeField] private int _numOfRopeSegments = 50;
    [SerializeField] private float _ropeSegmentsLength = 0.225f;

    [Header("Physics")]
    [SerializeField] private Vector2 _gravityForce = new Vector2(0, -2f);
    [SerializeField] private float _dampingFactor = 0.98f;//optinal
    [SerializeField] private LayerMask _collisionMask;
    [SerializeField] private float _collisionMaskRadius = 0.1f;
    [SerializeField] private float _maxPlayerSpeed = 10f; // 限制玩家最大速度

    [Header("Constraints")]
    [SerializeField] private int _numOfRopeConstraintRuns = 50;
    [Header("Optimizations")]
    [SerializeField] private int _collisionSegmentInterval = 2;
    [SerializeField] private int _previousNumOfRopeSegments; // 用于记录上一次的段数

    [Header("Player")]
    [SerializeField] private Transform _player; // 角色位置
    [SerializeField] private float _hookSpeed = 10f; // 钩索发射速度
    [SerializeField] private KeyCode _fireHookKey = KeyCode.Mouse0; // 钩索发射键
    [SerializeField] private KeyCode _retractHookKey = KeyCode.W; // 收缩钩索
    [SerializeField] private KeyCode _extendHookKey = KeyCode.S; // 伸长钩索
    [SerializeField] private KeyCode _swingLeftKey = KeyCode.A; // 左摆动
    [SerializeField] private KeyCode _swingRightKey = KeyCode.D; // 右摆动
    [SerializeField] private KeyCode _releaseHookKey = KeyCode.Space; // 释放钩索

    private Vector2 _hookTarget; // 钩索目标点
    private bool _hookAttached = false; // 钩索是否附着
    private bool _hookFired = false; // 钩索是否发射
    private Rigidbody2D _playerRigidbody; // 玩家的刚体组件

    private LineRenderer _lineRenderer;
    private List<RopeSegment> _ropeSegments = new List<RopeSegment>();
    private Vector3 _ropeStartpoint;

    private void Awake()
    {
        // 初始化 LineRenderer
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 0; // 初始状态下没有绳索段

        // 确保 _ropeSegments 列表为空
        _ropeSegments.Clear();

        // 获取玩家的 Rigidbody2D 组件
        _playerRigidbody = _player.GetComponent<Rigidbody2D>();
        
        // 设置玩家的碰撞检测模式为连续动态检测，防止高速穿墙
        if (_playerRigidbody != null)
        {
            _playerRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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

        // 钩索发射逻辑
        if (Input.GetKeyDown(_fireHookKey) && !_hookFired)
        {
            FireHook();
        }

        // 释放钩索
        if (Input.GetKeyDown(_releaseHookKey) && (_hookFired || _hookAttached))
        {
            ResetHook();
        }

        if (_hookFired && !_hookAttached)
        {
            MoveHook();
        }

        if (_hookAttached)
        {
            HandlePlayerControls();
        }
    }

    #region PlayerMethods
    private void FireHook()
    {
        _hookFired = true;
        _hookTarget = _player.position; // 从角色位置发射钩索

        // 动态创建绳索段
        _ropeSegments.Clear();
        Vector3 startPoint = _player.position; // 绳索起点为玩家位置
        for (int i = 0; i < _numOfRopeSegments; i++)
        {
            _ropeSegments.Add(new RopeSegment(startPoint));
            startPoint.y -= _ropeSegmentsLength;
        }

        // 更新 LineRenderer 的点数
        _lineRenderer.positionCount = _numOfRopeSegments;
    }

    private void MoveHook()
    {
        // 模拟钩索移动
        Vector2 direction = (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - _hookTarget;
        _hookTarget += direction.normalized * _hookSpeed * Time.deltaTime;

        // 检测是否命中目标
        RaycastHit2D hit = Physics2D.Raycast(_player.position, direction, Vector2.Distance(_player.position, _hookTarget), _collisionMask);
        if (hit.collider != null)
        {
            _hookTarget = hit.point;
            _hookAttached = true;
            _hookFired = false;
            AttachHook();
        }
    }

    private void AttachHook()
    {
        // 取出 RopeSegment 的副本
        RopeSegment firstSegment = _ropeSegments[0];

        // 修改副本的 CurrentPosition
        firstSegment.CurrentPosition = _hookTarget;

        // 将修改后的副本写回列表
        _ropeSegments[0] = firstSegment;
    }

    private void ResetHook()
    {
        _hookFired = false;
        _hookAttached = false;
        _ropeSegments.Clear();
        _lineRenderer.positionCount = 0; // 清空 LineRenderer
    }

    private void HandlePlayerControls()
    {
        // 收缩钩索（W键）
        if (Input.GetKey(_retractHookKey))
        { 
            _ropeSegmentsLength = Mathf.Max(0.1f, _ropeSegmentsLength - Time.deltaTime);
            UpdateRopeSegments();
        }

        // 伸长钩索（S键）
        if (Input.GetKey(_extendHookKey))
        {
            _ropeSegmentsLength += Time.deltaTime;
            UpdateRopeSegments();
        }

        // 左右摆动（A/D键）
        if (Input.GetKey(_swingLeftKey))
        {
            _playerRigidbody.AddForce(Vector2.left * 5f);
        }

        if (Input.GetKey(_swingRightKey))
        {
            _playerRigidbody.AddForce(Vector2.right * 5f);
        }

        // 限制玩家最大速度，防止高速穿墙
        if (_playerRigidbody.velocity.magnitude > _maxPlayerSpeed)
        {
            _playerRigidbody.velocity = _playerRigidbody.velocity.normalized * _maxPlayerSpeed;
        }
    }
    #endregion

    #region RopeMethods
    private void DrawRope()
    {
        if (_ropeSegments.Count == 0)
            return;
            
        Vector3[] ropePositions = new Vector3[_ropeSegments.Count];
        for (int i = 0; i < _ropeSegments.Count; i++)
        {
            ropePositions[i] = _ropeSegments[i].CurrentPosition;
        }
        _lineRenderer.positionCount = _ropeSegments.Count;
        _lineRenderer.SetPositions(ropePositions);
    }

    private void FixedUpdate()
    {
        // 如果钩索未发射或未附着，则跳过物理模拟
        if (!_hookFired && !_hookAttached)
            return;

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
        // 保持第一个点固定在钩索目标点
        RopeSegment firstSegment = _ropeSegments[0];
        firstSegment.CurrentPosition = _hookTarget;
        _ropeSegments[0] = firstSegment;

        // 处理其余绳子段的约束
        for (int i = 0; i < _ropeSegments.Count - 1; i++)
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

        // 处理最后一个绳索段与玩家之间的约束
        // 不再直接设置最后一个段为玩家位置，而是让玩家受到绳索的牵引
        if (_ropeSegments.Count > 0)
        {
            RopeSegment lastSegment = _ropeSegments[_ropeSegments.Count - 1];
            Vector2 playerPos = _player.position;
            
            float dist = Vector2.Distance(lastSegment.CurrentPosition, playerPos);
            
            // 如果玩家与最后一个绳索段的距离超过了绳索段长度
            if (dist > _ropeSegmentsLength)
            {
                // 计算玩家应该被拉到的位置
                Vector2 direction = (lastSegment.CurrentPosition - playerPos).normalized;
                Vector2 targetPosition = playerPos + direction * (dist - _ropeSegmentsLength);
                
                // 使用 MovePosition 移动玩家，这样可以考虑物理碰撞
                _playerRigidbody.MovePosition(targetPosition);
                
                // 检查玩家是否与墙壁发生碰撞
                HandlePlayerCollision();
            }
        }
    }

    private void HandlePlayerCollision()
    {
        // 检测玩家是否与墙壁发生碰撞
        Collider2D[] colliders = Physics2D.OverlapCircleAll(_player.position, 0.5f, _collisionMask);
        foreach (Collider2D collider in colliders)
        {
            Vector2 closestPoint = collider.ClosestPoint(_player.position);
            float distance = Vector2.Distance(_player.position, closestPoint);
            
            if (distance < 0.5f)
            {
                Vector2 normal = ((Vector2)_player.position - closestPoint).normalized;
                if (normal == Vector2.zero)
                {
                    normal = ((Vector2)_player.position - (Vector2)collider.transform.position).normalized;
                }
                
                float depth = 0.5f - distance;
                
                // 将玩家推离墙壁
                _player.position += (Vector3)(normal * depth);
            }
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
        if (_ropeSegments.Count == 0) return; // 如果绳索未创建，直接返回

        float ropeLength = Vector2.Distance(_player.position, _hookTarget);
        _numOfRopeSegments = Mathf.Max(2, Mathf.CeilToInt(ropeLength / _ropeSegmentsLength));

        // 如果新的段数大于当前段数，添加新的 RopeSegment
        while (_ropeSegments.Count < _numOfRopeSegments)
        {
            Vector3 lastSegmentPosition = _ropeSegments[_ropeSegments.Count - 1].CurrentPosition;
            Vector3 direction = (_player.position - lastSegmentPosition).normalized;
            _ropeSegments.Add(new RopeSegment(lastSegmentPosition + direction * _ropeSegmentsLength));
        }

        // 如果新的段数小于当前段数，移除多余的 RopeSegment
        while (_ropeSegments.Count > _numOfRopeSegments)
        {
            _ropeSegments.RemoveAt(_ropeSegments.Count - 1);
        }
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
    #endregion
}