using UnityEngine;
public interface IMechanicAction
{
    /// <summary>
    /// 激活行为
    /// </summary>
    void Activate();
    
    /// <summary>
    /// 停止行为
    /// </summary>
    void Deactivate();
    
    /// <summary>
    /// 检查行为是否正在执行
    /// </summary>
    bool IsActive { get; }
}