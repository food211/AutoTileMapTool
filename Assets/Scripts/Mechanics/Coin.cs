using UnityEngine;

public class Coin : MonoBehaviour, ISaveable
{
  private SpriteRenderer spriteRenderer;
  private bool isCollected = false;
  
  [Tooltip("金币的唯一ID，格式建议：场景名_位置坐标")]
  [SerializeField] private string coinID;
  
  [Header("动画设置")]
  public Sprite[] animationFrames;
  public float frameRate = 10f;
  
  [Header("收集效果")]
  public float collectDuration = 0.5f;
  public float jumpHeight = 1f;
  
  private void Awake()
  {
      if (string.IsNullOrEmpty(coinID))
      {
          string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
          Vector3 pos = transform.position;
          coinID = $"{sceneName}_Coin_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
      }
  }
  
  void Start()
  {
      spriteRenderer = GetComponent<SpriteRenderer>();
  }
  
  void Update()
  {
      // 只有未收集的金币才播放动画
      if (!isCollected && animationFrames.Length > 0)
      {
          int frameIndex = (int)(Time.time * frameRate) % animationFrames.Length;
          spriteRenderer.sprite = animationFrames[frameIndex];
      }
  }
  
  void OnTriggerEnter2D(Collider2D other)
  {
      if (isCollected) return;
      
      if (other.CompareTag("Player"))
      {
          Collect();
      }
  }
  
  private void Collect()
  {
      isCollected = true;
      
      // 禁用碰撞体
      Collider2D col = GetComponent<Collider2D>();
      if (col != null)
      {
          col.enabled = false;
      }
      
      // 保存状态
      if (ProgressManager.Instance != null)
      {
          ProgressManager.Instance.SaveObject(this);
      }
      
      // 播放收集动画
      StartCoroutine(PlayCollectAnimation());
  }
  
  private System.Collections.IEnumerator PlayCollectAnimation()
  {
      float elapsed = 0f;
      Vector3 startPos = transform.position;
      Vector3 originalScale = transform.localScale;
      Color originalColor = spriteRenderer.color;
      
      while (elapsed < collectDuration)
      {
          float t = elapsed / collectDuration;
          
          // 跳跃动画
          float jumpOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
          transform.position = startPos + Vector3.up * jumpOffset;
          
          // 缩放动画
          float scale = Mathf.Lerp(1f, 1.5f, t);
          transform.localScale = originalScale * scale;
          
          // 透明度动画
          float alpha = Mathf.Lerp(1f, 0f, t);
          spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
          
          elapsed += Time.deltaTime;
          yield return null;
      }
      
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
          
          if (isCollected)
          {
              gameObject.SetActive(false);
          }
      }
  }
  
  #endregion
}