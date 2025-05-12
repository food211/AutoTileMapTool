using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HideOnEditor : MonoBehaviour
{
    [SerializeField] private string editorSortingLayerName = "Back";
    [SerializeField] private string runtimeSortingLayerName = "Front";
    [SerializeField] private int editorOrderInLayer = 0;
    [SerializeField] private int runtimeOrderInLayer = 0;
    
    private Renderer[] renderers;
    private SpriteRenderer[] spriteRenderers;
    private TilemapRenderer tilemapRenderer;
    
    // 在编辑器中调用
    private void OnValidate()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ApplyEditorSortingLayer();
        }
        #endif
    }
    
    // 游戏启动时调用
    private void Awake()
    {
        ApplyRuntimeSortingLayer();
    }
    
    // 应用编辑器中的排序层
    private void ApplyEditorSortingLayer()
    {
        // 获取所有渲染器组件
        CollectRenderers();
        
        // 应用编辑器排序层
        ApplySortingLayer(editorSortingLayerName, editorOrderInLayer);
    }
    
    // 应用运行时的排序层
    private void ApplyRuntimeSortingLayer()
    {
        // 获取所有渲染器组件
        CollectRenderers();
        
        // 应用运行时排序层
        ApplySortingLayer(runtimeSortingLayerName, runtimeOrderInLayer);
    }
    
    // 收集所有渲染器组件
    private void CollectRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        tilemapRenderer = GetComponent<TilemapRenderer>();
    }
    
    // 应用排序层到所有渲染器
    private void ApplySortingLayer(string layerName, int orderInLayer)
    {
        // 确保排序层名称存在
        if (!SortingLayerExists(layerName))
        {
            Debug.LogWarning($"Sorting layer '{layerName}' does not exist! Using default layer instead.");
            layerName = "Default";
        }
        
        // 应用到所有常规渲染器
        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && !(renderer is SpriteRenderer) && !(renderer is TilemapRenderer))
                {
                    renderer.sortingLayerName = layerName;
                    renderer.sortingOrder = orderInLayer;
                }
            }
        }
        
        // 应用到所有精灵渲染器
        if (spriteRenderers != null)
        {
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.sortingLayerName = layerName;
                    spriteRenderer.sortingOrder = orderInLayer;
                }
            }
        }
        
        // 应用到Tilemap渲染器
        if (tilemapRenderer != null)
        {
            tilemapRenderer.sortingLayerName = layerName;
            tilemapRenderer.sortingOrder = orderInLayer;
        }
    }
    
    // 检查排序层是否存在
    private bool SortingLayerExists(string layerName)
    {
        SortingLayer[] layers = SortingLayer.layers;
        foreach (SortingLayer layer in layers)
        {
            if (layer.name == layerName)
                return true;
        }
        return false;
    }
}