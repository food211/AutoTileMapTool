using System.Collections.Generic;
using UnityEngine;
using ObjectPoolClass;

[System.Serializable]
public class ParallaxLayer
{
[Header("基础设置")]
public GameObject spritePrefab;  // 改为预制体，用于对象池

[Header("视差设置")]
[Range(0f, 1f)]
public float xParallaxSpeed = 0.5f;

[Range(0f, 0.5f)]
public float yParallaxSpeed = 0.1f;

public float maxYOffset = 3f;

[Header("无限循环")]
public bool enableInfiniteX = true;

// 内部变量
[System.NonSerialized]
public Vector2 startPosition;
[System.NonSerialized]
public float spriteWidth;
[System.NonSerialized]
public float currentYOffset;

// 三张图片管理
[System.NonSerialized]
public GameObject leftSprite;
[System.NonSerialized]
public GameObject centerSprite;
[System.NonSerialized]
public GameObject rightSprite;

[System.NonSerialized]
public bool isInitialized = false;
}

public class InfiniteParallaxController : MonoBehaviour
{
[Header("摄像机")]
public Camera targetCamera;

[Header("视差层")]
public ParallaxLayer[] parallaxLayers;

[Header("自动Z轴计算")]
public bool autoCalculateFromZ = true;
public float referenceZ = 0f;
public float maxBackgroundZ = 10f;

[Header("对象池预热")]
[Tooltip("每种预制体预热的数量")]
public int prewarmCount = 6;

private Vector2 lastCameraPosition;

void Start()
{
    // 初始化摄像机
    if (targetCamera == null)
        targetCamera = Camera.main ?? FindObjectOfType<Camera>();
    
    // 预热对象池
    PrewarmObjectPools();
    
    // 初始化视差层
    InitializeParallaxLayers();
    
    if (targetCamera != null)
        lastCameraPosition = targetCamera.transform.position;
}

void PrewarmObjectPools()
{
    foreach (var layer in parallaxLayers)
    {
        if (layer.spritePrefab != null)
        {
            ObjectPool.Instance.PrewarmPool(layer.spritePrefab, prewarmCount);
        }
    }
}

void InitializeParallaxLayers()
{
    foreach (var layer in parallaxLayers)
    {
        if (layer.spritePrefab == null) continue;
        
        // 获取预制体的精灵信息来计算宽度
        SpriteRenderer prefabRenderer = layer.spritePrefab.GetComponent<SpriteRenderer>();
        if (prefabRenderer?.sprite != null)
        {
            Vector2 spriteSize = prefabRenderer.sprite.bounds.size;
            Vector3 scale = layer.spritePrefab.transform.localScale;
            layer.spriteWidth = spriteSize.x * scale.x;
        }
        
        // 自动计算视差速度
        if (autoCalculateFromZ)
        {
            float layerZ = layer.spritePrefab.transform.position.z;
            float zDistance = Mathf.Abs(layerZ - referenceZ);
            float maxDistance = Mathf.Abs(maxBackgroundZ - referenceZ);
            
            if (maxDistance > 0)
            {
                layer.xParallaxSpeed = 1f - Mathf.Clamp01(zDistance / maxDistance);
                layer.yParallaxSpeed = layer.xParallaxSpeed * 0.2f;
            }
        }
        
        // 设置初始位置（以摄像机当前位置为基准）
        Vector3 cameraPos = targetCamera.transform.position;
        layer.startPosition = new Vector2(cameraPos.x, layer.spritePrefab.transform.position.y);
        
        // 创建三张图片
        CreateThreeSprites(layer);
    }
}

void CreateThreeSprites(ParallaxLayer layer)
{
    if (layer.spriteWidth <= 0) return;
    
    Vector3 basePosition = new Vector3(layer.startPosition.x, layer.startPosition.y, layer.spritePrefab.transform.position.z);
    
    // 创建左、中、右三张图片
    layer.leftSprite = ObjectPool.Instance.GetObject(layer.spritePrefab, 
        basePosition + Vector3.left * layer.spriteWidth, 
        layer.spritePrefab.transform.rotation);
        
    layer.centerSprite = ObjectPool.Instance.GetObject(layer.spritePrefab, 
        basePosition, 
        layer.spritePrefab.transform.rotation);
        
    layer.rightSprite = ObjectPool.Instance.GetObject(layer.spritePrefab, 
        basePosition + Vector3.right * layer.spriteWidth, 
        layer.spritePrefab.transform.rotation);
    
    layer.isInitialized = true;
}

void Update()
{
    if (targetCamera == null) return;
    
    Vector2 currentCameraPosition = targetCamera.transform.position;
    Vector2 cameraMovement = currentCameraPosition - lastCameraPosition;
    
    foreach (var layer in parallaxLayers)
    {
        if (!layer.isInitialized) continue;
        
        // 移动三张图片 - 现在包含Y轴跟随
        MoveLayerSprites(layer, cameraMovement);
        
        // 检查是否需要切换图片
        if (layer.enableInfiniteX)
        {
            CheckAndSwitchSprites(layer, currentCameraPosition.x);
        }
        
        // Y轴偏移计算 - 改为实时跟随模式
        CalculateYOffset(layer, cameraMovement);
        
        // 应用Y轴偏移到所有图片
        ApplyYOffset(layer);
    }
    
    lastCameraPosition = currentCameraPosition;
}

void MoveLayerSprites(ParallaxLayer layer, Vector2 cameraMovement)
{
    // X轴视差移动 + Y轴跟随移动
    Vector3 parallaxMovement = new Vector3(
        cameraMovement.x * layer.xParallaxSpeed, 
        cameraMovement.y,  // Y轴完全跟随摄像机
        0
    );
    
    if (layer.leftSprite != null)
        layer.leftSprite.transform.position += parallaxMovement;
    if (layer.centerSprite != null)
        layer.centerSprite.transform.position += parallaxMovement;
    if (layer.rightSprite != null)
        layer.rightSprite.transform.position += parallaxMovement;
}

void CalculateYOffset(ParallaxLayer layer, Vector2 cameraMovement)
{
    // 如果摄像机没有Y轴移动，偏移归零
    if (Mathf.Abs(cameraMovement.y) < 0.001f)
    {
        layer.currentYOffset = 0f;
    }
    else
    {
        // 基于当前帧的移动速度计算偏移，不累积
        float targetOffset = cameraMovement.y * layer.yParallaxSpeed;
        layer.currentYOffset = Mathf.Clamp(targetOffset, -layer.maxYOffset, layer.maxYOffset);
    }
}

void CheckAndSwitchSprites(ParallaxLayer layer, float cameraX)
{
    // 检查摄像机中心点是否进入左图或右图范围
    float leftSpriteX = layer.leftSprite.transform.position.x;
    float centerSpriteX = layer.centerSprite.transform.position.x;
    float rightSpriteX = layer.rightSprite.transform.position.x;
    
    float halfWidth = layer.spriteWidth * 0.5f;
    
    // 摄像机进入右图范围 - 卸载左图，在右侧创建新图
    if (cameraX >= rightSpriteX - halfWidth && cameraX <= rightSpriteX + halfWidth)
    {
        // 回收最左边的图片
        ObjectPool.Instance.ReturnObject(layer.leftSprite);
        
        // 重新排列：center -> left, right -> center
        layer.leftSprite = layer.centerSprite;
        layer.centerSprite = layer.rightSprite;
        
        // 在右侧创建新图片
        Vector3 newPosition = layer.rightSprite.transform.position + Vector3.right * layer.spriteWidth;
        layer.rightSprite = ObjectPool.Instance.GetObject(layer.spritePrefab, newPosition, layer.spritePrefab.transform.rotation);
    }
    // 摄像机进入左图范围 - 卸载右图，在左侧创建新图
    else if (cameraX >= leftSpriteX - halfWidth && cameraX <= leftSpriteX + halfWidth)
    {
        // 回收最右边的图片
        ObjectPool.Instance.ReturnObject(layer.rightSprite);
        
        // 重新排列：center -> right, left -> center
        layer.rightSprite = layer.centerSprite;
        layer.centerSprite = layer.leftSprite;
        
        // 在左侧创建新图片
        Vector3 newPosition = layer.leftSprite.transform.position + Vector3.left * layer.spriteWidth;
        layer.leftSprite = ObjectPool.Instance.GetObject(layer.spritePrefab, newPosition, layer.spritePrefab.transform.rotation);       
    }
}

void ApplyYOffset(ParallaxLayer layer)
{
    if (layer.leftSprite != null)
    {
        Vector3 pos = layer.leftSprite.transform.position;
        pos.y += layer.currentYOffset;
        layer.leftSprite.transform.position = pos;
    }
    
    if (layer.centerSprite != null)
    {
        Vector3 pos = layer.centerSprite.transform.position;
        pos.y += layer.currentYOffset;
        layer.centerSprite.transform.position = pos;
    }
    
    if (layer.rightSprite != null)
    {
        Vector3 pos = layer.rightSprite.transform.position;
        pos.y += layer.currentYOffset;
        layer.rightSprite.transform.position = pos;
    }
}

void OnDestroy()
{
    // 清理所有创建的图片
    foreach (var layer in parallaxLayers)
    {
        if (layer.leftSprite != null)
            ObjectPool.Instance.ReturnObject(layer.leftSprite);
        if (layer.centerSprite != null)
            ObjectPool.Instance.ReturnObject(layer.centerSprite);
        if (layer.rightSprite != null)
            ObjectPool.Instance.ReturnObject(layer.rightSprite);
    }
}

// 重新计算精灵尺寸
[ContextMenu("Recalculate Sizes")]
public void RecalculateSizes()
{
    foreach (var layer in parallaxLayers)
    {
        if (layer.spritePrefab != null)
        {
            SpriteRenderer prefabRenderer = layer.spritePrefab.GetComponent<SpriteRenderer>();
            if (prefabRenderer?.sprite != null)
            {
                Vector2 spriteSize = prefabRenderer.sprite.bounds.size;
                Vector3 scale = layer.spritePrefab.transform.localScale;
                layer.spriteWidth = spriteSize.x * scale.x;
            }
        }
    }
}
}