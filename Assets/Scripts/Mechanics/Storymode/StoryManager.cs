using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Mechanics/Story/Story Manager")]
public class StoryManager : Reciever
{
    [System.Serializable]
    public enum StoryActionType
    {
        ShowLetterbox,
        HideLetterbox,
        FollowPoint,
        ZoomCamera,
        ResetCamera,
        Wait
    }

    [System.Serializable]
    public class StoryAction
    {
        public StoryActionType actionType;
        public float duration = 1.0f;
        
        // 黑边相关参数
        [Range(0.05f, 0.3f)]
        public float letterboxHeight = 0.1f;
        
        // 兴趣点相关参数
        public bool useTargetObject = false;
        public GameObject targetObject;
        public Vector2 position;
        public bool dynamicTracking = true;
        
        // 缩放相关参数
        public float zoomSize = 5f;
    }

    [Header("故事序列设置")]
    [SerializeField] private string storySequenceId = "story_sequence";
    [SerializeField] private List<StoryAction> storyActions = new List<StoryAction>();
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool resetCameraOnComplete = true;

    [Header("事件")]
    public UnityEvent onStorySequenceStarted;
    public UnityEvent onStorySequenceCompleted;

    private CameraManager cameraManager;
    private Coroutine storyCoroutine;
    private bool isPlaying = false;
    private bool waitingForPointOfInterest = false;

    protected void Awake()
    {
        // 强制设置激活类型为Activate
        activationType = ActivationType.Activate;
    }
    
    protected new void Start()
    {
        base.Start(); // 调用基类的Start方法

        // 获取相机管理器
        cameraManager = FindObjectOfType<CameraManager>();
        if (cameraManager == null)
        {
            Debug.LogError("场景中没有找到CameraManager组件！");
        }

        // 如果设置了在开始时播放，则播放故事序列
        if (playOnStart)
        {
            PlayStorySequence();
        }

        // 订阅故事序列完成事件
        GameEvents.OnPointsOfInterestSequenceCompleted += HandlePointsOfInterestSequenceCompleted;
    }

    protected void OnDestroy()
    {
        // 取消订阅事件
        GameEvents.OnPointsOfInterestSequenceCompleted -= HandlePointsOfInterestSequenceCompleted;
        
        // 停止协程
        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
        }
    }

    // 重写Reciever的ActivateActions方法
    public override void ActivateActions()
    {
        base.ActivateActions();
        PlayStorySequence();
    }

    // 播放故事序列
    public void PlayStorySequence()
    {
        if (isPlaying)
        {
            StopStorySequence();
        }

        if (storyActions.Count == 0)
        {
            Debug.LogWarning("故事序列中没有定义任何动作！");
            return;
        }

        isPlaying = true;
        storyCoroutine = StartCoroutine(PlayStorySequenceCoroutine());
        onStorySequenceStarted?.Invoke();
    }

    // 停止故事序列
    public void StopStorySequence()
    {
        if (!isPlaying)
            return;

        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
            storyCoroutine = null;
        }

        // 重置相机
        if (resetCameraOnComplete && cameraManager != null)
        {
            // 隐藏黑边
            HideLetterbox();
            
            // 重置缩放
            cameraManager.ResetZoom();
        }

        isPlaying = false;
    }

    // 故事序列协程
    private IEnumerator PlayStorySequenceCoroutine()
    {
        // 执行每个故事动作
        for (int i = 0; i < storyActions.Count; i++)
        {
            StoryAction action = storyActions[i];
            
            switch (action.actionType)
            {
                case StoryActionType.ShowLetterbox:
                    ShowLetterbox(action.letterboxHeight, action.duration);
                    break;
                    
                case StoryActionType.HideLetterbox:
                    HideLetterbox(action.duration);
                    break;
                    
                case StoryActionType.FollowPoint:
                    FollowPoint(action);
                    if (waitingForPointOfInterest)
                    {
                        // 等待兴趣点序列完成
                        yield return new WaitUntil(() => !waitingForPointOfInterest);
                    }
                    break;
                    
                case StoryActionType.ZoomCamera:
                    if (cameraManager != null)
                    {
                        cameraManager.ZoomTo(action.zoomSize);
                    }
                    break;
                    
                case StoryActionType.ResetCamera:
                    if (cameraManager != null)
                    {
                        cameraManager.ResetZoom();
                    }
                    break;
                    
                case StoryActionType.Wait:
                    // 等待指定时间
                    yield return new WaitForSeconds(action.duration);
                    break;
            }
            
            // 等待动作完成
            if (action.actionType != StoryActionType.Wait)
            {
                yield return new WaitForSeconds(action.duration);
            }
        }

        // 故事序列完成
        CompleteStorySequence();
    }

    // 显示黑边
    private void ShowLetterbox(float height, float duration)
    {
        // 设置黑边高度并触发黑边显示事件
        if (cameraManager != null)
        {
            // 直接设置CameraManager中的黑边高度
            cameraManager.SetTargetHeight(height);
        }

        // 触发黑边显示事件，传递持续时间
        GameEvents.TriggerStoryModeChanged(true, duration);
    }

    // 隐藏黑边
    private void HideLetterbox(float duration = 0.5f)
    {
        // 触发黑边隐藏事件，传递持续时间
        GameEvents.TriggerStoryModeChanged(false, duration);
    }

    // 跟随兴趣点
    private void FollowPoint(StoryAction action)
    {
        // 创建兴趣点
        PointOfInterest poi = new PointOfInterest();
        poi.position = action.position;
        poi.stayTime = action.duration;
        poi.useTargetObject = action.useTargetObject;
        poi.targetObject = action.targetObject;
        poi.dynamicTracking = action.dynamicTracking;
        poi.hasCustomZoom = true;
        poi.zoomSize = action.zoomSize;

        // 设置一个标志，表示正在等待兴趣点序列完成
        waitingForPointOfInterest = true;
        
        // 创建兴趣点列表
        List<PointOfInterest> points = new List<PointOfInterest> { poi };
        
        // 触发跟随兴趣点事件
        GameEvents.TriggerFollowPointsOfInterest(points, storySequenceId);
    }

    // 处理兴趣点序列完成事件
    private void HandlePointsOfInterestSequenceCompleted(string sequenceId)
    {
        if (sequenceId == storySequenceId && waitingForPointOfInterest)
        {
            waitingForPointOfInterest = false;
            // 不直接调用CompleteStorySequence，让协程继续执行
        }
    }

    // 完成故事序列
    private void CompleteStorySequence()
    {
        isPlaying = false;
        storyCoroutine = null;
        
        // 如果需要重置相机，则重置
        if (resetCameraOnComplete && cameraManager != null)
        {
            // 隐藏黑边
            HideLetterbox();
            
            // 重置缩放
            cameraManager.ResetZoom();
        }
        
        // 触发完成事件
        onStorySequenceCompleted?.Invoke();
    }

    // 编辑器辅助方法：添加新的故事动作
    public void AddStoryAction(StoryActionType actionType)
    {
        StoryAction newAction = new StoryAction();
        newAction.actionType = actionType;
        storyActions.Add(newAction);
    }
}