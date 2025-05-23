using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerData
{
    // 基本属性
    public int currentHealth;
    public int maxHealth;
    
    // 位置信息
    public string currentScene;
    public float positionX;
    public float positionY;
    public float positionZ;
    
    // 收集物品
    public List<string> collectedItems = new List<string>();
    
    // 解锁的能力
    public List<string> unlockedAbilities = new List<string>();
    
    // 游戏进度 - 使用可序列化的类型替代Dictionary
    [SerializeField] private List<ObjectiveData> objectives = new List<ObjectiveData>();
    
    // 金币收集状态 - 使用可序列化的列表替代HashSet
    [SerializeField] private List<string> _collectedCoins = new List<string>();
    
    // 存档点信息
    public string lastCheckpointID;
    
    // 游戏时间
    public float playTime;
    
    // 玩家统计数据
    public int deaths;
    public int jumps;
    
    // 属性访问器 - 提供HashSet接口但内部使用List
    public HashSet<string> collectedCoins 
    {
        get 
        {
            // 如果内部列表为null，初始化它
            if (_collectedCoins == null)
                _collectedCoins = new List<string>();
                
            // 创建一个临时HashSet用于返回
            HashSet<string> result = new HashSet<string>(_collectedCoins);
            return result;
        }
    }
    
    // 添加金币到收集列表
    public void AddCoin(string coinID)
    {
        // 如果内部列表为null，初始化它
        if (_collectedCoins == null)
            _collectedCoins = new List<string>();
            
        if (!_collectedCoins.Contains(coinID))
        {
            _collectedCoins.Add(coinID);
        }
    }
    
    // 从PlayerController获取数据
    public void PopulateFromPlayer(PlayerController player, PlayerHealthManager healthManager)
    {
        if (player != null)
        {
            // 保存位置
            positionX = player.transform.position.x;
            positionY = player.transform.position.y;
            positionZ = player.transform.position.z;
            
            // 可以添加其他PlayerController中的数据
            // 例如：energy = player.energy;
        }
        
        if (healthManager != null)
        {
            currentHealth = healthManager.currentHealth;
            maxHealth = healthManager.maxHealth;
        }
        
        // 保存当前场景
        currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }
}

// 可序列化的目标数据结构
[Serializable]
public struct ObjectiveData
{
    public string id;
    public bool completed;
}