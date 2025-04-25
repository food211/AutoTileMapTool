using UnityEngine;
using System.Collections;

// 兴趣点数据结构
[System.Serializable]
public class PointOfInterest
{
    [Tooltip("兴趣点位置")]
    public Vector2 position;
    
    [Tooltip("在该点停留的时间（秒）")]
    public float stayTime = 1f;
    
    [Tooltip("移动到该点的过渡时间（秒）")]
    public float transitionTime = 1f;
    
    [Tooltip("是否使用目标对象作为位置")]
    public bool useTargetObject = true;
    
    [Tooltip("目标对象（如果启用）")]
    public GameObject targetObject;
    
    [Tooltip("是否在运行时动态跟踪目标对象")]
    public bool dynamicTracking = true;
}