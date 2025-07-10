using System;

/// <summary>
/// 可保存对象的接口，所有需要保存状态的游戏对象都应实现此接口
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// 获取对象的唯一ID
    /// </summary>
    string GetUniqueID();
    
    /// <summary>
    /// 保存对象状态
    /// </summary>
    /// <returns>可序列化的对象状态数据</returns>
    SaveData Save();
    
    /// <summary>
    /// 加载对象状态
    /// </summary>
    /// <param name="data">之前保存的状态数据</param>
    void Load(SaveData data);
}

/// <summary>
/// 可序列化的通用保存数据结构
/// </summary>
[Serializable]
public class SaveData
{
    // 对象类型，用于加载时识别
    public string objectType;
    
    // 通用状态字段
    public bool boolValue;
    public bool boolValue2;
    public int intValue;
    public float floatValue;
    public string stringValue;
    
    // 可以添加更多字段或使用字典来存储任意键值对
    // 但要注意Unity的JSON序列化限制
}