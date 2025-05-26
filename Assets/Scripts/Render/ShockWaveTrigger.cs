using UnityEngine;
using System.Collections;

public class ShockWaveTrigger : MonoBehaviour
{
    [SerializeField] private float _shockWaveTime = 0.75f;
    [SerializeField] private float _startPosition = -0.1f;
    [SerializeField] private float _endPosition = 1.0f;
    [SerializeField] private string _shaderPropertyName = "_WaveDistanceFromCenter";
    
    private Coroutine _shockWaveCoroutine;
    private Material _material;
    private SpriteRenderer _spriteRenderer;
    private int _waveDistanceFromCenterID;
    public bool _debugmode = false;

    private void Awake()
    {
        // 获取组件但不启用渲染
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 确保我们有一个材质实例
        _material = _spriteRenderer.material;
        
        // 获取shader属性ID
        _waveDistanceFromCenterID = Shader.PropertyToID(_shaderPropertyName);
        
        // 初始禁用渲染器
        _spriteRenderer.enabled = false;

        #if UNITY_EDITOR
        if(_debugmode)
        Debug.Log($"ShockWave初始化完成，使用属性: {_shaderPropertyName}");
        #endif
    }
    
    private IEnumerator ShockWaveAction(float startPos, float endPos)
    {
        // 启用渲染器开始效果
        _spriteRenderer.enabled = true;
        
        // 设置初始值
        _material.SetFloat(_waveDistanceFromCenterID, startPos);
        
        float elapsedTime = 0f;

#if UNITY_EDITOR
if(_debugmode)
Debug.Log($"开始ShockWave动画: 从 {startPos} 到 {endPos}, 持续 {_shockWaveTime} 秒");
#endif
        
        while (elapsedTime < _shockWaveTime)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / _shockWaveTime;
            float lerpedValue = Mathf.Lerp(startPos, endPos, normalizedTime);
            
            _material.SetFloat(_waveDistanceFromCenterID, lerpedValue);
            
            yield return null;
        }
        
        // 确保设置最终值
        _material.SetFloat(_waveDistanceFromCenterID, endPos);
        
        // 效果结束后禁用渲染器
        _spriteRenderer.enabled = false;

#if UNITY_EDITOR
if(_debugmode)
Debug.Log("ShockWave动画完成，已禁用渲染器");
#endif
    }

    void OnEnable()
    {
        GameEvents.OnPlayerReachedEndpointCenter += CallShockWave;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerReachedEndpointCenter -= CallShockWave;
        
        // 停止正在运行的协程
        if (_shockWaveCoroutine != null)
        {
            StopCoroutine(_shockWaveCoroutine);
            _shockWaveCoroutine = null;
        }
        
        // 确保渲染器被禁用
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }
    }

    public void CallShockWave(Transform endpointTransform)
    {
        // 如果已有协程在运行，先停止它
        if (_shockWaveCoroutine != null)
        {
            StopCoroutine(_shockWaveCoroutine);
        }
        
        _shockWaveCoroutine = StartCoroutine(ShockWaveAction(_startPosition, _endPosition));

#if UNITY_EDITOR
if(_debugmode)
Debug.Log("触发ShockWave效果");
#endif
    }
    
    // 可以手动触发的公共方法
    public void TriggerShockWave()
    {
        CallShockWave(null);
    }
    
    // 重置到初始状态
    public void ResetShockWave()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }
        
        if (_material != null)
        {
            _material.SetFloat(_waveDistanceFromCenterID, _startPosition);
        }
    }
}