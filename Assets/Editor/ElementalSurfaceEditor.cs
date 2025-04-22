using UnityEngine;
using UnityEditor;

public class ElementalSurfaceEditor : EditorWindow
{
    [MenuItem("Tools/Elemental Surfaces/Setup All Objects In Scene")]
    private static void SetupAllObjectsInScene()
    {
        // 找到场景中的所有元素表面
        IceSurface[] iceSurfaces = Object.FindObjectsOfType<IceSurface>();
        FireSurface[] fireSurfaces = Object.FindObjectsOfType<FireSurface>();
        ElectricSurface[] electricSurfaces = Object.FindObjectsOfType<ElectricSurface>();
        
        int totalCount = 0;
        
        // 设置所有冰面
        foreach (IceSurface iceSurface in iceSurfaces)
        {
            SetupIceSurface(iceSurface.gameObject, iceSurface);
            totalCount++;
        }
        
        // 设置所有火面
        foreach (FireSurface fireSurface in fireSurfaces)
        {
            SetupFireSurface(fireSurface.gameObject, fireSurface);
            totalCount++;
        }
        
        // 设置所有电面
        foreach (ElectricSurface electricSurface in electricSurfaces)
        {
            SetupElectricSurface(electricSurface.gameObject, electricSurface);
            totalCount++;
        }
        
        if (totalCount > 0)
        {
            EditorUtility.DisplayDialog("Setup Complete", $"Successfully setup {totalCount} elemental surfaces in the scene.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("No Elemental Surfaces", "No elemental surface components found in the scene.", "OK");
        }
    }
    
    [MenuItem("Tools/Elemental Surfaces/Setup Selected Objects")]
    private static void SetupSelectedObjects()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select at least one object to setup.", "OK");
            return;
        }
        
        int setupCount = 0;
        
        foreach (GameObject obj in selectedObjects)
        {
            bool wasSetup = false;
            
            // 检查是否有任何元素表面脚本
            IceSurface iceSurface = obj.GetComponent<IceSurface>();
            FireSurface fireSurface = obj.GetComponent<FireSurface>();
            ElectricSurface electricSurface = obj.GetComponent<ElectricSurface>();
            
            // 设置冰面
            if (iceSurface != null)
            {
                SetupIceSurface(obj, iceSurface);
                wasSetup = true;
            }
            
            // 设置火面
            if (fireSurface != null)
            {
                SetupFireSurface(obj, fireSurface);
                wasSetup = true;
            }
            
            // 设置电面
            if (electricSurface != null)
            {
                SetupElectricSurface(obj, electricSurface);
                wasSetup = true;
            }
            
            if (wasSetup)
            {
                setupCount++;
            }
        }
        
        if (setupCount > 0)
        {
            EditorUtility.DisplayDialog("Setup Complete", $"Successfully setup {setupCount} object(s).", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("No Elemental Surfaces", "None of the selected objects have elemental surface components attached.", "OK");
        }
    }
    
    [MenuItem("Tools/Elemental Surfaces/Add Ice Surface")]
    private static void AddIceSurface()
    {
        AddElementalSurface<IceSurface>("Ice");
    }
    
    [MenuItem("Tools/Elemental Surfaces/Add Fire Surface")]
    private static void AddFireSurface()
    {
        AddElementalSurface<FireSurface>("Fire");
    }
    
    [MenuItem("Tools/Elemental Surfaces/Add Electric Surface")]
    private static void AddElectricSurface()
    {
        AddElementalSurface<ElectricSurface>("Elect");
    }
    
    private static void AddElementalSurface<T>(string tag) where T : Component
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select at least one object to add component.", "OK");
            return;
        }
        
        int addedCount = 0;
        
        foreach (GameObject obj in selectedObjects)
        {
            // 检查是否已经有该组件
            if (obj.GetComponent<T>() == null)
            {
                T component = obj.AddComponent<T>();
                obj.tag = tag;
                addedCount++;
                
                // 根据类型设置组件
                if (typeof(T) == typeof(IceSurface))
                {
                    SetupIceSurface(obj, component as IceSurface);
                }
                else if (typeof(T) == typeof(FireSurface))
                {
                    SetupFireSurface(obj, component as FireSurface);
                }
                else if (typeof(T) == typeof(ElectricSurface))
                {
                    SetupElectricSurface(obj, component as ElectricSurface);
                }
            }
        }
        
        if (addedCount > 0)
        {
            EditorUtility.DisplayDialog("Components Added", $"Successfully added {typeof(T).Name} to {addedCount} object(s).", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("No Components Added", "All selected objects already have the component.", "OK");
        }
    }
    
    private static void SetupIceSurface(GameObject obj, IceSurface iceSurface)
    {
        // 确保有碰撞体
        Collider2D collider = obj.GetComponent<Collider2D>();
        if (collider == null)
        {
            collider = obj.AddComponent<BoxCollider2D>();
        }
        
        // 设置标签
        obj.tag = "Ice";
        
        // 获取SpriteRenderer
        SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 应用冰面视觉效果
            Material material = new Material(spriteRenderer.sharedMaterial);
            spriteRenderer.material = material;
            
            // 检查材质是否支持这些属性
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.5f);
            }
            
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.8f, 0.95f, 1f, 0.8f));
            }
            
            // 如果使用URP或HDRP，可以设置额外的属性
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.5f);
            }
        }
        
        // 如果不是触发器，创建并应用物理材质
        if (!iceSurface.isTrigger)
        {
            PhysicsMaterial2D iceMaterial = new PhysicsMaterial2D("Ice");
            iceMaterial.friction = 0.05f;
            iceMaterial.bounciness = 0.1f;
            
            collider.sharedMaterial = iceMaterial;
        }
        
        // 标记为已修改
        EditorUtility.SetDirty(obj);
    }
    
    private static void SetupFireSurface(GameObject obj, FireSurface fireSurface)
    {
        // 确保有碰撞体
        Collider2D collider = obj.GetComponent<Collider2D>();
        if (collider == null)
        {
            collider = obj.AddComponent<BoxCollider2D>();
        }
        
        // 设置标签
        obj.tag = "Fire";
        
        // 获取SpriteRenderer
        SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 应用火焰视觉效果
            Material material = new Material(spriteRenderer.sharedMaterial);
            spriteRenderer.material = material;
            
            // 检查材质是否支持这些属性
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.1f);
            }
            
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.5f, 0.1f, 0.2f, 1f));
            }
            
            // 如果使用URP或HDRP，可以设置额外的属性
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.1f);
            }
        }
        
        // 标记为已修改
        EditorUtility.SetDirty(obj);
    }
    
    private static void SetupElectricSurface(GameObject obj, ElectricSurface electricSurface)
    {
        // 确保有碰撞体
        Collider2D collider = obj.GetComponent<Collider2D>();
        if (collider == null)
        {
            collider = obj.AddComponent<BoxCollider2D>();
        }
        
        // 设置标签
        obj.tag = "Elect";
        
        // 获取SpriteRenderer
        SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 应用电击视觉效果
            Material material = new Material(spriteRenderer.sharedMaterial);
            spriteRenderer.material = material;
            
            // 检查材质是否支持这些属性
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(0.3f, 0.7f, 1f, 0.9f) * 1.5f);
                material.EnableKeyword("_EMISSION");
            }
            
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.3f, 0.7f, 1f, 0.9f));
            }
        }
        
        // 标记为已修改
        EditorUtility.SetDirty(obj);
    }
    
    // 移除了原来的RefreshAllElementalSurfaces方法，因为它现在与SetupAllObjectsInScene功能重复
}