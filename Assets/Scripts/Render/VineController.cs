using UnityEditor;
using UnityEngine;

public class SimpleVine : MonoBehaviour
{
    [Header("基础设置")]
    [Min(1)]
    public int segmentCount = 8;
    [Min(0.01f)]
    public float segmentLength = 0.3f;

    [Header("效果参数")]
    public float pushForce = 10f;
    public float windStrength = 0.5f;
    public float damping = 0.9f;
    public float restoreSpeed = 6f;

    [Header("柔软度设置")]
    public float flexibility = 0.8f;
    public float downwardPropagation = 1.2f;
    public float upwardPropagation = 0.3f;

    [Header("弯曲设置")]
    [Range(0f, 1f)]
    public float bendStrength = 0.2f;       // 弯曲强度
    [Range(0f, 2f)]
    public float bendFrequency = 1f;        // 弯曲频率
    [Range(-1f, 1f)]
    public float bendDirection = 0f;        // 弯曲方向 (-1到1)
    public enum BendShape                    // 弯曲形状
    {
        Sine,                               // 正弦波
        Arc,                                // 弧形
        Zigzag,                             // 锯齿形
        SShape                              // S形
    }
    public BendShape bendShape = BendShape.Sine;
    public bool invertBend = false;         // 是否反转弯曲

    [Header("视觉设置")]
    public float colliderRadius = 0.15f;
    public Color startColor = Color.green;
    public Color endColor = new Color(0f, 0.39f, 0f);
    public float lineWidth = 0.1f;

    [Header("风力设置")]
    public float windSpeed = 1f;
    public float windVariation = 0.3f;
    public float segmentDelay = 0.2f;
    [Range(0f, 2f)]
    public float noiseStrength = 0.5f;      // 噪声强度

    [Header("随机化设置")]
    [Range(0f, 1f)]
    public float randomVariation = 0.3f; // 随机变化强度
    public bool autoRandomizeOnStart = true; // 是否自动随机化
    [Header("编辑器设置")]
    public bool showPreviewInEditor = true;  // 是否在编辑器中显示预览
    public bool useLineRendererInEditor = true; // 是否在编辑器中使用LineRenderer预览

    private LineRenderer line;
    private Vector3[] positions;
    private Vector3[] velocities;
    private Vector3[] originalPositions;
    private float[] windOffsets;

    // 每根藤蔓的独特属性
    private float uniqueScale;
    private float uniqueWindPhase;
    private float uniqueBendDirection;
    private float uniqueBendStrength;
    private float uniqueBendFrequency;
    private BendShape uniqueBendShape;
    private Color uniqueStartColor;
    private Color uniqueEndColor;
    private float uniqueSegmentLength;
    private float uniqueWindSpeed;
    private float uniqueNoiseStrength;      // 独特的噪声强度

    void Start()
    {
        ValidateParameters();
        // 生成独特属性
        GenerateUniqueProperties();

        // 初始化LineRenderer
        InitializeLineRenderer();

        // 初始化位置数组
        positions = new Vector3[segmentCount];
        velocities = new Vector3[segmentCount];
        originalPositions = new Vector3[segmentCount];
        windOffsets = new float[segmentCount];

        // 设置初始位置
        InitializePositions();

        // 创建2D碰撞检测
        CreateColliders2D();
        UpdateLine();
    }
    void ValidateParameters()
    {
        segmentCount = Mathf.Max(1, segmentCount);
        segmentLength = Mathf.Max(0.01f, segmentLength);
    }

    void InitializeLineRenderer()
    {
        line = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
        line.positionCount = segmentCount;
        line.useWorldSpace = true;
        line.startWidth = lineWidth * (uniqueScale > 0 ? uniqueScale : 1f);
        line.endWidth = lineWidth * (uniqueScale > 0 ? uniqueScale : 1f) * 0.3f;

        // 设置独特的渐变颜色
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(uniqueStartColor != null ? uniqueStartColor : startColor, 0.0f),
                                     new GradientColorKey(uniqueEndColor != null ? uniqueEndColor : endColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        line.colorGradient = gradient;
    }

    void InitializePositions()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            // 计算弯曲偏移
            float bendOffset = CalculateBendOffset(i);

            Vector3 pos = transform.position + Vector3.down * (i * uniqueSegmentLength) + Vector3.right * bendOffset;
            positions[i] = pos;
            originalPositions[i] = pos;
            windOffsets[i] = Random.Range(0f, Mathf.PI * 2f) + uniqueWindPhase;
        }
    }

    float CalculateBendOffset(int segmentIndex)
    {
        float progress = (float)segmentIndex / segmentCount;
        float direction = invertBend ? -1f : 1f;
        direction *= uniqueBendDirection;

        // 根据选择的形状计算弯曲
        switch (uniqueBendShape)
        {
            case BendShape.Sine:
                // 正弦波形
                return Mathf.Sin(progress * Mathf.PI * uniqueBendFrequency) * uniqueBendStrength * direction;

            case BendShape.Arc:
                // 弧形 (抛物线)
                return (progress * (1 - progress) * 4) * uniqueBendStrength * direction;

            case BendShape.Zigzag:
                // 锯齿形
                return Mathf.PingPong(progress * uniqueBendFrequency * 2, 1f) * uniqueBendStrength * direction;

            case BendShape.SShape:
                // S形
                return Mathf.Sin(progress * Mathf.PI * 2 * uniqueBendFrequency) * uniqueBendStrength * direction;

            default:
                return 0;
        }
    }

    void GenerateUniqueProperties()
    {
        if (!autoRandomizeOnStart)
        {
            // 如果不自动随机化，使用设置的值
            uniqueScale = 1f;
            uniqueWindPhase = 0f;
            uniqueBendDirection = bendDirection;
            uniqueBendStrength = bendStrength;
            uniqueBendFrequency = bendFrequency;
            uniqueBendShape = bendShape;
            uniqueSegmentLength = segmentLength;
            uniqueWindSpeed = windSpeed;
            uniqueStartColor = startColor;
            uniqueEndColor = endColor;
            uniqueNoiseStrength = noiseStrength;  // 使用设置的噪声强度
            return;
        }

        // 使用位置作为随机种子，确保相同位置的藤蔓总是相同
        Random.InitState((int)(transform.position.x * 1000 + transform.position.y * 1000 + transform.position.z * 1000));

        // 尺寸变化 (0.7 - 1.3倍)
        uniqueScale = 1f + Random.Range(-randomVariation * 0.5f, randomVariation * 0.5f);

        // 独特的风相位
        uniqueWindPhase = Random.Range(0f, Mathf.PI * 2f);

        // 弯曲方向 (-1 到 1)
        uniqueBendDirection = bendDirection + Random.Range(-randomVariation, randomVariation);
        uniqueBendDirection = Mathf.Clamp(uniqueBendDirection, -1f, 1f);

        // 弯曲强度
        uniqueBendStrength = bendStrength * (1f + Random.Range(-randomVariation * 0.5f, randomVariation * 0.5f));

        // 弯曲频率
        uniqueBendFrequency = bendFrequency * (1f + Random.Range(-randomVariation * 0.3f, randomVariation * 0.3f));

        // 弯曲形状 (随机选择形状)
        if (Random.value < randomVariation * 0.5f)
        {
            int shapeIndex = Random.Range(0, System.Enum.GetValues(typeof(BendShape)).Length);
            uniqueBendShape = (BendShape)shapeIndex;
        }
        else
        {
            uniqueBendShape = bendShape;
        }

        // 段长度变化
        uniqueSegmentLength = segmentLength * (1f + Random.Range(-randomVariation * 0.3f, randomVariation * 0.3f));

        // 风速变化
        uniqueWindSpeed = windSpeed * (1f + Random.Range(-randomVariation * 0.4f, randomVariation * 0.4f));

        // 噪声强度变化
        uniqueNoiseStrength = noiseStrength * (1f + Random.Range(-randomVariation * 0.3f, randomVariation * 0.3f));

        // 颜色变化
        float colorVariation = randomVariation * 0.3f;
        uniqueStartColor = VaryColor(startColor, colorVariation);
        uniqueEndColor = VaryColor(endColor, colorVariation);

        // 恢复随机种子
        Random.InitState((int)(Time.realtimeSinceStartup * 1000));
    }

    Color VaryColor(Color baseColor, float variation)
    {
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);

        // 轻微调整色相、饱和度和亮度
        h = (h + Random.Range(-variation, variation)) % 1f;
        s = Mathf.Clamp01(s + Random.Range(-variation * 0.5f, variation * 0.5f));
        v = Mathf.Clamp01(v + Random.Range(-variation * 0.3f, variation * 0.3f));

        return Color.HSVToRGB(h, s, v);
    }

    void CreateColliders2D()
    {
        for (int i = 1; i < segmentCount; i++)
        {
            GameObject col = new GameObject($"Segment_{i}");
            col.transform.SetParent(transform);
            col.transform.position = positions[i];

            CircleCollider2D circle = col.AddComponent<CircleCollider2D>();
            circle.radius = colliderRadius * uniqueScale;
            circle.isTrigger = true;

            VineSegment2D segment = col.AddComponent<VineSegment2D>();
            segment.vineIndex = i;
            segment.vine = this;
        }
    }

    void Update()
    {
        // 使用独特的风力参数
        for (int i = 1; i < segmentCount; i++)
        {
            float influence = (float)i / segmentCount;
            float delay = i * segmentDelay;

            // 使用独特的风速和相位
            float mainWind = Mathf.Sin(Time.time * uniqueWindSpeed + windOffsets[i]) * windStrength * influence;

            // 添加变化和噪音
            float variation = Mathf.Sin(Time.time * uniqueWindSpeed * 2.3f + delay) * windVariation * influence;

            // 使用独特的噪声强度
            float noise = Mathf.PerlinNoise(Time.time * 0.5f + i * 0.1f + uniqueWindPhase, 0) * windVariation * uniqueNoiseStrength * influence;

            velocities[i].x += (mainWind + variation + noise) * Time.deltaTime;

            // 轻微的Y轴摆动
            velocities[i].y += Mathf.Sin(Time.time * uniqueWindSpeed * 1.7f + delay) * windStrength * 0.3f * influence * Time.deltaTime;
        }

        // 物理模拟
        for (int i = 1; i < segmentCount; i++)
        {
            // 应用速度
            positions[i] += velocities[i] * Time.deltaTime;

            // 长度约束（使用独特长度）
            Vector3 direction = (positions[i] - positions[i - 1]).normalized;
            positions[i] = positions[i - 1] + direction * uniqueSegmentLength;

            // 渐进式恢复力
            float restoreStrength = restoreSpeed * (1f - (float)i / segmentCount * 0.3f);
            Vector3 restore = (originalPositions[i] - positions[i]) * restoreStrength;
            velocities[i] += restore * Time.deltaTime;

            // 适度阻尼
            velocities[i] *= damping;

            // 更新碰撞体位置
            if (i - 1 < transform.childCount)
            {
                transform.GetChild(i - 1).position = positions[i];
            }
        }

        UpdateLine();
    }

    public void Push(int index, Vector3 force)
    {
        if (index > 0 && index < segmentCount)
        {
            // 主要推力（考虑尺寸）
            velocities[index] += force * pushForce * uniqueScale;

            // 向下传播能量
            for (int i = index + 1; i < segmentCount; i++)
            {
                float distance = i - index;
                float influence = Mathf.Exp(-distance * 0.3f) * downwardPropagation;

                Vector3 downwardForce = force * influence;
                float delay = distance * 0.1f;
                downwardForce *= (1f + Mathf.Sin(Time.time * 10f + delay + uniqueWindPhase) * 0.2f);

                velocities[i] += downwardForce;
            }

            // 向上传播
            for (int i = index - 1; i > 0; i--)
            {
                float distance = index - i;
                float influence = Mathf.Exp(-distance * 0.8f) * upwardPropagation;

                Vector3 upwardForce = force * influence;
                velocities[i] += upwardForce;
            }
        }
    }

    void UpdateLine()
    {
        if (line != null && positions != null)
        {
            line.SetPositions(positions);
        }
    }


#if UNITY_EDITOR
    // 编辑器相关代码
    private void OnValidate()
    {
        // 当在编辑器中修改属性时更新预览
        if (EditorApplication.isPlaying)
            return;

        if (showPreviewInEditor)
        {
            // 生成临时预览属性
            GeneratePreviewProperties();

            // 如果启用了LineRenderer预览，则更新LineRenderer
            if (useLineRendererInEditor)
            {
                UpdateEditorLineRenderer();
            }
        }
    }

    private void GeneratePreviewProperties()
    {
        // 使用当前设置的值
        uniqueScale = 1f;
        uniqueWindPhase = 0f;
        uniqueBendDirection = bendDirection;
        uniqueBendStrength = bendStrength;
        uniqueBendFrequency = bendFrequency;
        uniqueBendShape = bendShape;
        uniqueSegmentLength = segmentLength;
        uniqueWindSpeed = windSpeed;
        uniqueStartColor = startColor;
        uniqueEndColor = endColor;
        uniqueNoiseStrength = noiseStrength;
    }

    private void UpdateEditorLineRenderer()
    {
        // 确保有LineRenderer组件
        if (line == null)
        {
            line = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
        }

        line.positionCount = segmentCount;
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth * 0.3f;

        // 设置渐变颜色
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(endColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        line.colorGradient = gradient;

        // 创建预览位置
        Vector3[] previewPositions = new Vector3[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            float progress = (float)i / segmentCount;
            float direction = invertBend ? -1f : 1f;
            direction *= bendDirection;

            float bendOffset = 0;
            switch (bendShape)
            {
                case BendShape.Sine:
                    bendOffset = Mathf.Sin(progress * Mathf.PI * bendFrequency) * bendStrength * direction;
                    break;
                case BendShape.Arc:
                    bendOffset = (progress * (1 - progress) * 4) * bendStrength * direction;
                    break;
                case BendShape.Zigzag:
                    bendOffset = Mathf.PingPong(progress * bendFrequency * 2, 1f) * bendStrength * direction;
                    break;
                case BendShape.SShape:
                    bendOffset = Mathf.Sin(progress * Mathf.PI * 2 * bendFrequency) * bendStrength * direction;
                    break;
            }

            previewPositions[i] = transform.position + Vector3.down * (i * segmentLength) + Vector3.right * bendOffset;
        }

        // 更新LineRenderer但不调用UpdateLine方法
        line.SetPositions(previewPositions);
    }
#endif

    // 可视化碰撞体
    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 如果在编辑器中且启用了预览，但禁用了LineRenderer预览，则使用Gizmos绘制
        if (showPreviewInEditor && !useLineRendererInEditor && !EditorApplication.isPlaying)
        {
            DrawGizmosPreview();
            return;
        }
#endif

        // 原有的OnDrawGizmos代码
        // 绘制藤蔓起点位置的绿色圆
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        if (positions != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 1; i < positions.Length; i++)
            {
                Gizmos.DrawWireSphere(positions[i], colliderRadius * (uniqueScale > 0 ? uniqueScale : 1f));
            }

            // 显示原始弯曲形状
            Gizmos.color = uniqueStartColor;
            for (int i = 1; i < originalPositions.Length; i++)
            {
                Gizmos.DrawLine(originalPositions[i - 1], originalPositions[i]);
            }
        }
        else
        {
            DrawGizmosPreview();
        }
    }

    // 新增方法：使用Gizmos绘制预览
    private void DrawGizmosPreview()
    {
        // 在编辑器中预览弯曲形状
        Gizmos.color = startColor;
        Vector3[] previewPositions = new Vector3[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            float progress = (float)i / segmentCount;
            float direction = invertBend ? -1f : 1f;
            direction *= bendDirection;

            float bendOffset = 0;
            switch (bendShape)
            {
                case BendShape.Sine:
                    bendOffset = Mathf.Sin(progress * Mathf.PI * bendFrequency) * bendStrength * direction;
                    break;
                case BendShape.Arc:
                    bendOffset = (progress * (1 - progress) * 4) * bendStrength * direction;
                    break;
                case BendShape.Zigzag:
                    bendOffset = Mathf.PingPong(progress * bendFrequency * 2, 1f) * bendStrength * direction;
                    break;
                case BendShape.SShape:
                    bendOffset = Mathf.Sin(progress * Mathf.PI * 2 * bendFrequency) * bendStrength * direction;
                    break;
            }

            previewPositions[i] = transform.position + Vector3.down * (i * segmentLength) + Vector3.right * bendOffset;

            if (i > 0)
            {
                Gizmos.DrawLine(previewPositions[i - 1], previewPositions[i]);
                Gizmos.DrawWireSphere(previewPositions[i], colliderRadius);
            }
        }
    }

    // 手动随机化（在编辑器中调用）
    [ContextMenu("随机化藤蔓")]
    public void RandomizeVine()
    {
        autoRandomizeOnStart = true;
        GenerateUniqueProperties();
        InitializePositions();

#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            // 在编辑器模式下，使用预览更新
            if (useLineRendererInEditor)
            {
                UpdateEditorLineRenderer();
            }
            return;
        }
#endif
        UpdateLine();
    }

        // 应用噪声设置（在编辑器中调用）
    [ContextMenu("应用噪声设置")]
    public void ApplyNoiseSettings()
    {
        uniqueNoiseStrength = noiseStrength;
    }

    // 重置为设置值（在编辑器中调用）
    [ContextMenu("使用当前设置")]
    public void UseCurrentSettings()
    {
        autoRandomizeOnStart = false;
        GenerateUniqueProperties();
        InitializePositions();

#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            // 在编辑器模式下，使用预览更新
            if (useLineRendererInEditor)
            {
                UpdateEditorLineRenderer();
            }
            return;
        }
#endif
        UpdateLine();
    }

    // 应用弯曲设置（在编辑器中调用）
    [ContextMenu("应用弯曲设置")]
    public void ApplyBendSettings()
    {
        uniqueBendDirection = bendDirection;
        uniqueBendStrength = bendStrength;
        uniqueBendFrequency = bendFrequency;
        uniqueBendShape = bendShape;

#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            // 在编辑器模式下，使用预览更新
            if (useLineRendererInEditor)
            {
                UpdateEditorLineRenderer();
            }
            return;
        }
#endif
        InitializePositions();
        UpdateLine();
    }
}


// 2D碰撞检测组件
public class VineSegment2D : MonoBehaviour
{
  public SimpleVine vine;
  public int vineIndex;
  
  void OnTriggerEnter2D(Collider2D other)
  {
      if (other.CompareTag("Player"))
      {
          Vector3 pushDir = (transform.position - other.transform.position).normalized;
          vine.Push(vineIndex, pushDir);
      }
  }
  
  void OnTriggerStay2D(Collider2D other)
  {
      if (other.CompareTag("Player"))
      {
          Vector3 pushDir = (transform.position - other.transform.position).normalized;
          vine.Push(vineIndex, pushDir * 0.2f);
      }
  }
}