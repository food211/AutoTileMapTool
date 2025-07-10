using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public class PointsOfInterestSequence : MonoBehaviour
{
    // 序列标识符
    [SerializeField] private string sequenceId = "Sequence1";
    
    // 兴趣点列表
    [SerializeField] private List<PointOfInterest> pointsOfInterest = new List<PointOfInterest>();
    
    // 触发类型
    public enum TriggerType
    {
        Manual,         // 手动触发
        OnStart,        // 游戏开始时触发
        OnTriggerEnter, // 进入触发区域时触发
        OnTriggerExit,  // 离开触发区域时触发
        OnCollision,    // 碰撞时触发
        OnKeyPress      // 按键触发
    }
    
    [Header("触发设置")]
    [SerializeField] private TriggerType triggerType = TriggerType.Manual;
    [SerializeField] private KeyCode triggerKey = KeyCode.Space; // 用于按键触发
    [SerializeField] private string triggerTag = "Player"; // 用于触发器和碰撞触发
    [SerializeField] private bool playOnce = true; // 是否只播放一次
    
    [Header("序列完成后执行")]
    [SerializeField] private bool activateGameObjectOnComplete = false;
    [SerializeField] private GameObject gameObjectToActivate;
    [SerializeField] private bool deactivateGameObjectOnComplete = false;
    [SerializeField] private GameObject gameObjectToDeactivate;
    
    // 私有变量
    private bool hasPlayed = false;
    private bool isWaitingForCompletion = false;
    
    private void Start()
    {
        // 订阅序列完成事件
        GameEvents.OnPointsOfInterestSequenceCompleted += OnSequenceCompleted;
        
        // 如果设置为游戏开始时触发，则立即播放
        if (triggerType == TriggerType.OnStart)
        {
            PlaySequence();
        }
        
        // 更新所有目标引用的位置（如果有）
        UpdateTargetPositions();
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        GameEvents.OnPointsOfInterestSequenceCompleted -= OnSequenceCompleted;
    }
    
    private void Update()
    {
        // 检查按键触发
        if (triggerType == TriggerType.OnKeyPress && Input.GetKeyDown(triggerKey))
        {
            PlaySequence();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerType == TriggerType.OnTriggerEnter && other.CompareTag(triggerTag))
        {
            PlaySequence();
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (triggerType == TriggerType.OnTriggerExit && other.CompareTag(triggerTag))
        {
            PlaySequence();
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (triggerType == TriggerType.OnCollision && collision.gameObject.CompareTag(triggerTag))
        {
            PlaySequence();
        }
    }
    
    // 更新所有目标引用的位置
    private void UpdateTargetPositions()
    {
        for (int i = 0; i < pointsOfInterest.Count; i++)
        {
            if (pointsOfInterest[i].useTargetObject && pointsOfInterest[i].targetObject != null)
            {
                // 将目标对象的世界位置转换为局部位置
                Vector3 worldPos = pointsOfInterest[i].targetObject.transform.position;
                Vector3 localPos = transform.InverseTransformPoint(worldPos);
                pointsOfInterest[i].position = new Vector2(localPos.x, localPos.y);
            }
        }
    }
    
    // 公开方法：手动播放序列
    public void PlaySequence()
    {
        // 如果设置为只播放一次且已经播放过，则返回
        if (playOnce && hasPlayed)
            return;
            
        // 如果正在等待序列完成，则返回
        if (isWaitingForCompletion)
            return;
            
        // 标记为已播放和正在等待完成
        hasPlayed = true;
        isWaitingForCompletion = true;
        
        // 更新所有目标引用的位置
        UpdateTargetPositions();
        
        // 触发兴趣点序列事件
        GameEvents.TriggerFollowPointsOfInterest(pointsOfInterest, sequenceId);
    }
    
    // 序列完成事件处理
    private void OnSequenceCompleted(string completedSequenceId)
    {
        // 检查是否是当前序列
        if (completedSequenceId != sequenceId)
            return;
            
        // 重置等待状态
        isWaitingForCompletion = false;
        
        // 执行完成后的操作
        if (activateGameObjectOnComplete && gameObjectToActivate != null)
        {
            gameObjectToActivate.SetActive(true);
        }
        
        if (deactivateGameObjectOnComplete && gameObjectToDeactivate != null)
        {
            gameObjectToDeactivate.SetActive(false);
        }
        
        // 可以在这里添加其他完成后的操作
        OnSequenceCompletedActions();
    }
    
    // 可以在子类中重写此方法以添加自定义完成后的操作
    protected virtual void OnSequenceCompletedActions()
    {
        // 默认不执行任何操作
    }
    
    // 重置序列状态，允许再次播放
    public void ResetSequence()
    {
        hasPlayed = false;
    }
}

#if UNITY_EDITOR
// 自定义编辑器，允许在场景视图中直接设置兴趣点位置
[CustomEditor(typeof(PointsOfInterestSequence))]
public class PointsOfInterestSequenceEditor : Editor
{
    private PointsOfInterestSequence sequence;
    private int selectedPointIndex = -1;
    private SerializedProperty pointsOfInterestProp;
    
    private void OnEnable()
    {
        sequence = (PointsOfInterestSequence)target;
        pointsOfInterestProp = serializedObject.FindProperty("pointsOfInterest");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // 绘制默认Inspector中的其他属性
        DrawPropertiesExcluding(serializedObject, new string[] { "pointsOfInterest" });
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("兴趣点列表", EditorStyles.boldLabel);
        
        // 绘制兴趣点列表
        for (int i = 0; i < pointsOfInterestProp.arraySize; i++)
        {
            SerializedProperty pointProp = pointsOfInterestProp.GetArrayElementAtIndex(i);
            SerializedProperty positionProp = pointProp.FindPropertyRelative("position");
            SerializedProperty stayTimeProp = pointProp.FindPropertyRelative("stayTime");
            SerializedProperty transitionTimeProp = pointProp.FindPropertyRelative("transitionTime");
            SerializedProperty useTargetObjectProp = pointProp.FindPropertyRelative("useTargetObject");
            SerializedProperty targetObjectProp = pointProp.FindPropertyRelative("targetObject");
            SerializedProperty dynamicTrackingProp = pointProp.FindPropertyRelative("dynamicTracking");
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("兴趣点 " + (i + 1), EditorStyles.boldLabel);
            
            // 删除按钮
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                pointsOfInterestProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return; // 退出函数，防止访问已删除的元素
            }
            
            // 选择按钮
            GUI.color = (selectedPointIndex == i) ? Color.green : Color.white;
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                selectedPointIndex = i;
                SceneView.RepaintAll();
            }
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // 是否使用目标对象
            EditorGUILayout.PropertyField(useTargetObjectProp, new GUIContent("使用目标对象"));
            
            if (useTargetObjectProp.boolValue)
            {
                // 目标对象字段
                EditorGUILayout.PropertyField(targetObjectProp, new GUIContent("目标对象"));
                
                // 动态跟踪选项
                EditorGUILayout.PropertyField(dynamicTrackingProp, new GUIContent("动态跟踪"));
                
                // 如果有目标对象，显示其位置信息
                if (targetObjectProp.objectReferenceValue != null)
                {
                    GameObject targetObj = (GameObject)targetObjectProp.objectReferenceValue;
                    Vector3 worldPos = targetObj.transform.position;
                    Vector3 localPos = sequence.transform.InverseTransformPoint(worldPos);
                    
                    // 更新位置属性
                    positionProp.vector2Value = new Vector2(localPos.x, localPos.y);
                    
                    EditorGUILayout.LabelField("目标位置: " + worldPos.ToString("F2"), EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("请选择一个目标对象", MessageType.Warning);
                }
            }
            else
            {
                // 手动位置设置
                EditorGUILayout.PropertyField(positionProp, new GUIContent("位置"));
            }
            
            // 停留时间和过渡时间
            EditorGUILayout.PropertyField(stayTimeProp, new GUIContent("停留时间"));
            EditorGUILayout.PropertyField(transitionTimeProp, new GUIContent("过渡时间"));
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        // 添加兴趣点按钮
        if (GUILayout.Button("添加兴趣点"))
        {
            pointsOfInterestProp.arraySize++;
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("编辑工具", EditorStyles.boldLabel);
        
        // 在场景中选择兴趣点提示
        EditorGUILayout.HelpBox("在场景视图中点击以设置所选兴趣点的位置。", MessageType.Info);
        
        // 从当前选择的GameObject创建兴趣点按钮
        if (GUILayout.Button("从选中对象创建兴趣点"))
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject != null && selectedObject != sequence.gameObject)
            {
                int newIndex = pointsOfInterestProp.arraySize;
                pointsOfInterestProp.arraySize++;
                
                SerializedProperty newPointProp = pointsOfInterestProp.GetArrayElementAtIndex(newIndex);
                SerializedProperty useTargetObjectProp = newPointProp.FindPropertyRelative("useTargetObject");
                SerializedProperty targetObjectProp = newPointProp.FindPropertyRelative("targetObject");
                
                useTargetObjectProp.boolValue = true;
                targetObjectProp.objectReferenceValue = selectedObject;
                
                selectedPointIndex = newIndex;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先在Hierarchy中选择一个GameObject。", "确定");
            }
        }
        
        // 测试播放序列按钮
        if (GUILayout.Button("测试播放序列"))
        {
            if (Application.isPlaying)
            {
                sequence.PlaySequence();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行模式下测试播放序列。", "确定");
            }
        }
        
        // 重置序列状态按钮
        if (GUILayout.Button("重置序列状态"))
        {
            if (Application.isPlaying)
            {
                sequence.ResetSequence();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "只能在运行模式下重置序列状态。", "确定");
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void OnSceneGUI()
    {
        // 如果没有选中任何点，不进行绘制
        if (pointsOfInterestProp.arraySize == 0)
            return;
        
        // 绘制所有兴趣点和它们之间的连线
        for (int i = 0; i < pointsOfInterestProp.arraySize; i++)
        {
            SerializedProperty pointProp = pointsOfInterestProp.GetArrayElementAtIndex(i);
            SerializedProperty positionProp = pointProp.FindPropertyRelative("position");
            SerializedProperty useTargetObjectProp = pointProp.FindPropertyRelative("useTargetObject");
            SerializedProperty targetObjectProp = pointProp.FindPropertyRelative("targetObject");
            
            Vector2 position = positionProp.vector2Value;
            
            // 将局部坐标转换为世界坐标
            Vector3 worldPosition = sequence.transform.TransformPoint(new Vector3(position.x, position.y, 0));
            
            // 如果使用目标对象且目标对象存在，则显示连接线
            if (useTargetObjectProp.boolValue && targetObjectProp.objectReferenceValue != null)
            {
                GameObject targetObj = (GameObject)targetObjectProp.objectReferenceValue;
                Vector3 targetPos = targetObj.transform.position;
                
                // 绘制从兴趣点到目标对象的虚线
                Handles.color = Color.cyan;
                Handles.DrawDottedLine(worldPosition, targetPos, 2f);
                
                // 在目标对象位置绘制小标记
                Handles.color = Color.blue;
                Handles.SphereHandleCap(0, targetPos, Quaternion.identity, 0.3f, EventType.Repaint);
            }
            
            // 绘制兴趣点
            Handles.color = (i == selectedPointIndex) ? Color.green : Color.yellow;
            if (Handles.Button(worldPosition, Quaternion.identity, 0.5f, 0.5f, Handles.SphereHandleCap))
            {
                selectedPointIndex = i;
                Repaint();
            }
            
            // 绘制序号
            Handles.Label(worldPosition + Vector3.up * 0.6f, "点 " + (i + 1));
            
            // 如果选中了某个点，显示移动手柄
            if (i == selectedPointIndex)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(worldPosition, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sequence, "Move Point of Interest");
                    
                    // 将世界坐标转换回局部坐标
                    Vector3 localPosition = sequence.transform.InverseTransformPoint(newPosition);
                    positionProp.vector2Value = new Vector2(localPosition.x, localPosition.y);
                    
                    // 如果使用目标对象，更新为手动模式
                    if (useTargetObjectProp.boolValue)
                    {
                        useTargetObjectProp.boolValue = false;
                    }
                    
                    serializedObject.ApplyModifiedProperties();
                }
            }
            
            // 绘制到下一个点的连线
            if (i < pointsOfInterestProp.arraySize - 1)
            {
                SerializedProperty nextPointProp = pointsOfInterestProp.GetArrayElementAtIndex(i + 1);
                SerializedProperty nextPositionProp = nextPointProp.FindPropertyRelative("position");
                Vector2 nextPosition = nextPositionProp.vector2Value;
                Vector3 nextWorldPosition = sequence.transform.TransformPoint(new Vector3(nextPosition.x, nextPosition.y, 0));
                
                Handles.color = Color.gray;
                Handles.DrawLine(worldPosition, nextWorldPosition);
                
                // 绘制方向箭头
                Vector3 direction = (nextWorldPosition - worldPosition).normalized;
                Vector3 arrowPos = worldPosition + direction * Vector3.Distance(worldPosition, nextWorldPosition) * 0.5f;
                float arrowSize = 0.3f;
                Handles.ArrowHandleCap(0, arrowPos, Quaternion.LookRotation(direction), arrowSize, EventType.Repaint);
            }
        }
        
        // 处理场景视图中的点击，用于放置兴趣点
        if (selectedPointIndex >= 0 && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            // 检查是否点击在UI上
            if (!EditorGUIUtility.hotControl.Equals(0))
                return;
                
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
            
            if (hit.collider != null)
            {
                UpdateSelectedPointPosition(hit.point);
                Event.current.Use();
            }
            else
            {
                // 如果没有碰撞，使用一个平面
                Plane plane = new Plane(Vector3.forward, Vector3.zero);
                float distance;
                if (plane.Raycast(ray, out distance))
                {
                    Vector3 hitPoint = ray.GetPoint(distance);
                    UpdateSelectedPointPosition(hitPoint);
                    Event.current.Use();
                }
            }
        }
    }
    
    private void UpdateSelectedPointPosition(Vector3 worldPosition)
    {
        SerializedProperty pointProp = pointsOfInterestProp.GetArrayElementAtIndex(selectedPointIndex);
        SerializedProperty positionProp = pointProp.FindPropertyRelative("position");
        SerializedProperty useTargetObjectProp = pointProp.FindPropertyRelative("useTargetObject");
        
        // 将世界坐标转换为局部坐标
        Vector3 localPosition = sequence.transform.InverseTransformPoint(worldPosition);
        positionProp.vector2Value = new Vector2(localPosition.x, localPosition.y);
        
        // 如果使用目标对象，更新为手动模式
        if (useTargetObjectProp.boolValue)
        {
            useTargetObjectProp.boolValue = false;
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif