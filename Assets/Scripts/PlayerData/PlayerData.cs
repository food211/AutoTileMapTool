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
    
    // 游戏进度
    public Dictionary<string, bool> completedObjectives = new Dictionary<string, bool>();
    
    // 存档点信息
    public string lastCheckpointID;
    
    // 游戏时间
    public float playTime;
    
    // 玩家统计数据
    public int deaths;
    public int jumps;
    
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