using UnityEngine;

public class Coin : MonoBehaviour, ISaveable
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
        
        // 不再需要手动检查收集状态，SaveManager会自动加载状态
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // 确保只触发一次
        if (isCollected) return;
        
        // 检查是否是玩家
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }
    
    private void Collect()
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
            ProgressManager.Instance.SaveObject(this);
        }
        
        // 如果没有动画组件，直接隐藏
        if (animator == null)
        {
            gameObject.SetActive(false);
        }
    }
    
    // 由Animation Event调用，在收集动画结束时
    public void DestroyAfterCollected()
    {
        gameObject.SetActive(false);
    }
    
    #region ISaveable Implementation
    
    public string GetUniqueID()
    {
        return coinID;
    }
    
    public SaveData Save()
    {
        SaveData data = new SaveData();
        data.objectType = "Coin";
        data.boolValue = isCollected;
        return data;
    }
    
    public void Load(SaveData data)
    {
        if (data != null && data.objectType == "Coin")
        {
            isCollected = data.boolValue;
            
            // 如果已收集，直接禁用游戏对象
            if (isCollected)
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    #endregion
}