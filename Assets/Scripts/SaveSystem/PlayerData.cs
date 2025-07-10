using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerData
{
    // 保留原有的基本属性
    public int currentHealth;
    public int maxHealth;

    // 位置信息
    public string currentScene;
    public float positionX;
    public float positionY;
    public float positionZ;

    // 收集物品
    public List<string> collectedItems = new List<string>();

    // 收集的金币
    public List<string> collectedCoins = new List<string>();

    // 解锁的能力
    public List<string> unlockedAbilities = new List<string>();

    // 游戏进度 - 使用可序列化的类型替代Dictionary
    [SerializeField] private List<ObjectiveData> objectives = new List<ObjectiveData>();

    // 存档点信息
    public string lastCheckpointID;

    // 游戏时间
    public float playTime;

    // 玩家统计数据
    public int deaths;
    public int jumps;

    // 新增：通用对象状态存储
    [SerializeField] private List<SaveableObjectData> saveableObjects = new List<SaveableObjectData>();

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

    // 添加金币到收集列表
    public void AddCoin(string coinID)
    {
        if (!collectedCoins.Contains(coinID))
        {
            collectedCoins.Add(coinID);
        }
    }

    // 保存可保存对象的状态
    public void SaveObject(string uniqueID, string objectType, SaveData data)
    {
        // 查找是否已存在此对象的数据
        int existingIndex = saveableObjects.FindIndex(obj => obj.uniqueID == uniqueID);

        if (existingIndex >= 0)
        {
            // 更新现有数据
            saveableObjects[existingIndex].data = data;
        }
        else
        {
            // 添加新数据
            saveableObjects.Add(new SaveableObjectData
            {
                uniqueID = uniqueID,
                objectType = objectType,
                data = data
            });
        }
    }

    // 获取可保存对象的状态
    public SaveData GetObjectData(string uniqueID)
    {
        SaveableObjectData objData = saveableObjects.Find(obj => obj.uniqueID == uniqueID);
        return objData?.data;
    }

    // 检查对象是否有保存的状态
    public bool HasObjectData(string uniqueID)
    {
        return saveableObjects.Exists(obj => obj.uniqueID == uniqueID);
    }

    /// <summary>
    /// 从玩家数据中移除特定对象的状态
    /// </summary>
    /// <param name="objectID">对象唯一ID</param>
    public void RemoveObjectData(string objectID)
    {
        if (saveableObjects != null && saveableObjects.Exists(obj => obj.uniqueID == objectID))
        {
            saveableObjects.RemoveAll(obj => obj.uniqueID == objectID);
        }
    }
}


// 可序列化的目标数据结构
[Serializable]
public struct ObjectiveData
{
    public string id;
    public bool completed;
}

// 可保存对象的数据结构
[Serializable]
public class SaveableObjectData
{
    public string uniqueID;     // 对象的唯一ID
    public string objectType;   // 对象类型
    public SaveData data;       // 对象状态数据
}