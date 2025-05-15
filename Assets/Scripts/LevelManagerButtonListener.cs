using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelManager按钮监听器 - 连接按钮到LevelManager单例
/// </summary>
public class LevelManagerButtonListener : MonoBehaviour
{
    public enum ButtonAction
    {
        StartNewGame,
        ContinueGame,
        ExitGame,
        ReloadCurrentLevel,
        LoadMainMenu,
        SaveAndExitGame
    }
    
    [SerializeField] private ButtonAction action = ButtonAction.StartNewGame;
    [SerializeField] private Button targetButton;
    
    private void Start()
    {
        // 如果没有指定按钮，尝试获取当前对象上的按钮
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
        
        // 设置按钮监听器
        if (targetButton != null)
        {
            targetButton.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogError("未找到目标按钮");
        }
    }
    
    private void OnButtonClicked()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogError("未找到LevelManager实例");
            return;
        }
        
        // 根据设置的动作类型调用相应的方法
        switch (action)
        {
            case ButtonAction.StartNewGame:
                LevelManager.Instance.StartNewGame();
                break;
                
            case ButtonAction.ContinueGame:
                LevelManager.Instance.ContinueGame();
                break;
                
            case ButtonAction.ExitGame:
                LevelManager.Instance.ExitGame();
                break;
                
            case ButtonAction.ReloadCurrentLevel:
                LevelManager.Instance.ReloadCurrentLevel();
                break;
                
            case ButtonAction.LoadMainMenu:
                LevelManager.Instance.LoadMainMenu();
                break;
                
            case ButtonAction.SaveAndExitGame:
                LevelManager.Instance.SaveAndExitGame();
                break;
        }
    }
    
    private void OnDestroy()
    {
        // 移除按钮监听器
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}