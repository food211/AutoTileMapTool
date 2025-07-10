using UnityEngine;

public class RopeCutter : MonoBehaviour
{
    [Header("切割设置")]
    [SerializeField] private float cutRadius = 0.5f; // 切割半径
    [SerializeField] private bool cutOnTriggerEnter = true; // 是否在触发器进入时切断
    [SerializeField] private bool cutOnCollision = false; // 是否在碰撞时切断
    [SerializeField] private LayerMask playerLayer; // 玩家层
    [SerializeField] private LayerMask ropeLayer; // 绳索层
    
    [Header("视觉效果")]
    [SerializeField] private GameObject cutEffectPrefab; // 切割效果预制体
    [SerializeField] private float effectDuration = 0.5f; // 效果持续时间
    [SerializeField] private bool showCutArea = true; // 是否显示切割区域
    [SerializeField] private Color cutAreaColor = new Color(1f, 0f, 0f, 0.2f); // 切割区域颜色
    
    [Header("音效")]
    [SerializeField] private AudioClip cutSound; // 切割音效
    [SerializeField] private float cutSoundVolume = 1f; // 切割音效音量
    
    // 内部变量
    private AudioSource audioSource;
    
    private void Awake()
    {
        // 获取或添加AudioSource组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && cutSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        // 如果没有设置玩家层，默认设置为Player层
        if (playerLayer.value == 0)
            playerLayer = LayerMask.GetMask("Player");
            
        // 如果没有设置绳索层，默认设置为Default层
        if (ropeLayer.value == 0)
            ropeLayer = LayerMask.GetMask("Default");
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!cutOnTriggerEnter) return;
        
        // 检查是否是玩家
        if ((playerLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            TryCutRope(collision.gameObject);
        }
        
        // 检查是否是绳索（如果绳索有碰撞体）
        if ((ropeLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            // 尝试获取绳索系统组件
            RopeSystem ropeSystem = collision.GetComponent<RopeSystem>();
            if (ropeSystem != null)
            {
                CutRopeAt(ropeSystem, transform.position);
            }
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!cutOnCollision) return;
        
        // 检查是否是玩家
        if ((playerLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            TryCutRope(collision.gameObject);
        }
    }
    
    // 尝试切断玩家的绳索
    private void TryCutRope(GameObject playerObject)
    {
        // 获取玩家控制器
        PlayerController player = playerObject.GetComponent<PlayerController>();
        if (player == null) return;
        
        // 获取绳索系统
        RopeSystem ropeSystem = player.GetComponentInChildren<RopeSystem>();
        if (ropeSystem == null) return;
        
        // 在当前位置切断绳索
        CutRopeAt(ropeSystem, transform.position);
    }
    
    // 在指定位置切断绳索
    private void CutRopeAt(RopeSystem ropeSystem, Vector2 position)
    {
        // 调用绳索系统的切断方法
        bool ropeCut = ropeSystem.CutRope(position, cutRadius);
        
        // 如果成功切断绳索
        if (ropeCut)
        {
            // 播放切割音效
            if (audioSource != null && cutSound != null)
            {
                audioSource.PlayOneShot(cutSound, cutSoundVolume);
            }
            
            // 显示切割效果
            ShowCutEffect(position);
        }
    }
    
    // 显示切割效果
    private void ShowCutEffect(Vector2 position)
    {
        if (cutEffectPrefab != null)
        {
            // 实例化切割效果
            GameObject effect = Instantiate(cutEffectPrefab, position, Quaternion.identity);
            
            // 销毁效果
            Destroy(effect, effectDuration);
        }
    }
    
    // 在编辑器中可视化切割区域
    private void OnDrawGizmos()
    {
        if (!showCutArea) return;
        
        Gizmos.color = cutAreaColor;
        Gizmos.DrawSphere(transform.position, cutRadius);
        
        // 绘制切割边缘
        Gizmos.color = Color.red;
        DrawCircle(transform.position, cutRadius, 16);
    }
    
    // 绘制圆形
    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angle = 0f;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * radius;
        
        for (int i = 0; i <= segments; i++)
        {
            angle += angleStep;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}