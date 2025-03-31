using UnityEngine;

public class AimIndicator : MonoBehaviour
{
    [SerializeField] private float indicatorLength = 2f;
    [SerializeField] private Color indicatorColor = Color.white;
    
    private LineRenderer lineRenderer;
    
    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            
        SetupLineRenderer();
    }
    
    private void SetupLineRenderer()
    {
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = indicatorColor;
        lineRenderer.endColor = indicatorColor;
    }
    
    private void Update()
    {
        // 设置指示器位置
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, transform.position + transform.right * indicatorLength);
    }
}
