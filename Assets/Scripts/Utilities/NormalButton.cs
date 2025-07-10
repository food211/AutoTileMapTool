using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class NormalButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    private float hoverScaleMultiplier = 1.2f;
    
    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private Vector3 _hoverScale;
    
    private bool _isHovered;
    private bool _initialized;
    private bool _isScaling;
    
    [SerializeField]
    private float scaleSpeed = 10f;
    
    private AudioClip _hoverSound;
    
    // 按钮组件引用
    private Button _button;
    private Selectable _selectable;

    private void InitializeIfNeeded()
    {
        if (_initialized) return;
        
        _rectTransform = transform as RectTransform;
        if (_rectTransform != null)
        {
            _originalScale = _rectTransform.localScale;
            _hoverScale = _originalScale * hoverScaleMultiplier;
            _targetScale = _originalScale;
            
            // 获取按钮组件
            _button = GetComponent<Button>();
            if (_button == null)
            {
                // 如果没有Button组件，尝试获取其他Selectable组件（如Toggle、Dropdown等）
                _selectable = GetComponent<Selectable>();
            }
            
            _initialized = true;
        }
    }

    private void Awake()
    {
        InitializeIfNeeded();
        _hoverSound = Resources.Load<AudioClip>("Sound/blop01");
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        
        if (_rectTransform != null)
        {
            _isHovered = false;
            _isScaling = false;
            _rectTransform.localScale = _originalScale;
            _targetScale = _originalScale;
        }
    }

    /// <summary>
    /// 检查按钮是否可交互
    /// </summary>
    private bool IsInteractable()
    {
        // 优先检查Button组件
        if (_button != null)
        {
            return _button.interactable;
        }
        // 如果没有Button组件，检查其他Selectable组件
        else if (_selectable != null)
        {
            return _selectable.interactable;
        }
        // 如果没有任何可交互组件，默认为可交互
        return true;
    }

    private void SetScaling(bool scaling)
    {
        // 只有当状态从false变为true时播放声音
        if (!_isScaling && scaling)
        {
            // if (_hoverSound != null && BgmCountroller.Instance != null)
            // {
            //     BgmCountroller.Instance.PlaySFX(_hoverSound);
            // }
        }
        _isScaling = scaling;
    }

    private void Update()
    {
        if (!_initialized || _rectTransform == null || !_isScaling) return;

        if (_rectTransform.localScale != _targetScale)
        {
            _rectTransform.localScale = Vector3.Lerp(
                _rectTransform.localScale, 
                _targetScale, 
                Time.deltaTime * scaleSpeed
            );

            if (Vector3.Distance(_rectTransform.localScale, _targetScale) < 0.001f)
            {
                _rectTransform.localScale = _targetScale;
                SetScaling(false);
            }
        }
        else
        {
            SetScaling(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_initialized || _isHovered) return;
        
        // 检查按钮是否可交互，如果不可交互则不放大
        if (!IsInteractable()) return;
        
        _isHovered = true;
        _targetScale = _hoverScale;
        SetScaling(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_initialized || !_isHovered) return;
        
        _isHovered = false;
        _targetScale = _originalScale;
        SetScaling(true);
    }

    private void OnDisable()
    {
        if (_initialized && _rectTransform != null)
        {
            _rectTransform.localScale = _originalScale;
            _isHovered = false;
            SetScaling(false);
        }
    }

    private void OnValidate()
    {
        if (_initialized)
        {
            _hoverScale = _originalScale * hoverScaleMultiplier;
        }
    }
    
    /// <summary>
    /// 当按钮状态变化时调用此方法（可由外部脚本调用）
    /// </summary>
    public void UpdateButtonState()
    {
        // 如果按钮变为不可交互，但当前正在悬停，则恢复原始大小
        if (_isHovered && !IsInteractable())
        {
            _isHovered = false;
            _targetScale = _originalScale;
            SetScaling(true);
        }
    }
}