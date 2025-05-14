using UnityEngine;

[RequireComponent(typeof(RegionManager))]
public class RegionEffectUpdater : MonoBehaviour
{
    [Header("Animation Settings")]
    public bool animateEdgeGlow = false;
    public float glowMinValue = 0.8f;
    public float glowMaxValue = 1.5f;
    public float glowSpeed = 1.0f;
    
    private RegionManager regionManager;
    
    private void Start()
    {
        regionManager = GetComponent<RegionManager>();
    }
    
    private void Update()
    {
        if (animateEdgeGlow)
        {
            float t = (Mathf.Sin(Time.time * glowSpeed) + 1) * 0.5f;
            regionManager.edgeGlow = Mathf.Lerp(glowMinValue, glowMaxValue, t);
            
            // 更新材质
            Material material = regionManager.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().material;
            if (material != null)
            {
                material.SetFloat("_EdgeGlow", regionManager.edgeGlow);
            }
        }
    }
}
