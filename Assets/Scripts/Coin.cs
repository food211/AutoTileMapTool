using UnityEngine;

public class Coin : MonoBehaviour
{
    private Animator animator;
    private bool isCollected = false;
    
    [Tooltip("金币的唯一ID，格式建议：场景名_位置坐标")]
    [SerializeField] private string coinID;
    
    private void Awake()
    {
        // 如果没有设置ID，自动生成一个
        if (string.IsNullOrEmpty(coinID))
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Vector3 pos = transform.position;
            coinID = $"{sceneName}_Coin_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
        }
    }
    
    void Start()
    {
        animator = GetComponent<Animator>();
        
        // 订阅场景完全加载事件
        GameEvents.OnSceneFullyLoaded += CheckCollectionStatus;
        
        // 立即检查一次
        CheckCollectionStatus();
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        GameEvents.OnSceneFullyLoaded -= CheckCollectionStatus;
    }
    
    private void CheckCollectionStatus(string sceneName = null)
    {
        // 确保ProgressManager实例存在
        if (ProgressManager.Instance != null)
        {
            // 检查是否已收集
            if (ProgressManager.Instance.IsCoinCollected(coinID))
            {
                gameObject.SetActive(false);
                isCollected = true;
            }
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // 确保只触发一次
        if (isCollected) return;
        
        // 检查是否是玩家
        if (other.CompareTag("Player"))
        {
            isCollected = true;
            
            // 触发收集动画
            if (animator != null)
            {
                animator.SetTrigger("Collect");
            }
            
            // 禁用碰撞体防止重复触发
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
            
            // 保存收集状态
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.SaveCoinCollected(coinID);
            }
            
            // 如果没有动画组件，直接隐藏
            if (animator == null)
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    // 由Animation Event调用，在收集动画结束时
    public void DestroyAfterCollected()
    {
        gameObject.SetActive(false);
    }
}