// VFX控制脚本
using UnityEngine;
using UnityEngine.VFX;

public class RotatingStarlightVFX : MonoBehaviour
{
    [Header("星光动画设置")]
    [SerializeField] private VisualEffect highlightVFX;
    private float maxSize = 1f; // 最大尺寸
    
    [Header("触发设置")]
    [SerializeField] private float activationRange = 20f;
    [SerializeField] private bool autoPlay = true;
    
    private Transform playerTransform;
    private bool isPlaying = false;
    
    void Start()
    {
        // 查找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        
        if (autoPlay)
                StartStarlight();
    }
    
    void Update()
    {
        if (!autoPlay)
            CheckPlayerDistance();
    }

    
    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;
        
        float distance = Vector2.Distance(transform.position, playerTransform.position);
        bool playerInRange = distance <= activationRange;
        
        if (playerInRange && !isPlaying)
        {
            StartStarlight();
        }
        else if (!playerInRange && isPlaying)
        {
            StopStarlight();
        }
    }
    
    public void StartStarlight()
    {
        if (highlightVFX != null && !isPlaying)
        {
            highlightVFX.Play();
            isPlaying = true;
        }
    }
    
    public void StopStarlight()
    {
        if (highlightVFX != null && isPlaying)
        {
            highlightVFX.Stop();
            isPlaying = false;
        }
    }
    
    // 道具被收集时调用
    public void OnItemCollected()
    {
        StopStarlight();
    }
    
    void OnDrawGizmosSelected()
    {
        if (!autoPlay)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationRange);
        }
        
        // 显示最大尺寸范围
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxSize);
    }
}
