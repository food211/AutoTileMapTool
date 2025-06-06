using System.Collections.Generic;
using UnityEngine;

namespace ObjectPoolClass
{
    public class ObjectPool : MonoBehaviour
    {
        // 单例模式
        private static ObjectPool instance;
        public static ObjectPool Instance
        {
            get
            {
                if (instance == null)
                {
                    // 如果实例不存在，创建一个新的游戏对象并添加ObjectPool组件
                    GameObject obj = new GameObject("ObjectPool");
                    instance = obj.AddComponent<ObjectPool>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }

        // 存储不同类型对象的池
        private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();
        
        // 预热对象池 - 创建指定数量的对象，但不设置父对象层级
        public void PrewarmPool(GameObject prefab, int count)
        {
            if (prefab == null) return;
            
            string key = prefab.name;

            // 如果这种类型的对象池不存在，创建一个
            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary[key] = new Queue<GameObject>();
            }

            // 创建指定数量的对象并放入池中
            for (int i = 0; i < count; i++)
            {
                GameObject obj = Instantiate(prefab);
                obj.name = key;
                obj.SetActive(false);
                // 将对象设为对象池的子对象，便于管理
                obj.transform.SetParent(transform);
                poolDictionary[key].Enqueue(obj);
            }
        }

        // 获取对象 - 不会自动设置父对象，由调用者决定
        public GameObject GetObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            
            string key = prefab.name;

            // 如果这种类型的对象池不存在，创建一个
            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary[key] = new Queue<GameObject>();
            }

            GameObject obj;

            // 如果池中有可用对象，取出使用
            if (poolDictionary[key].Count > 0)
            {
                obj = poolDictionary[key].Dequeue();
                // 设置位置和旋转
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                
                // 注意：不设置父对象，由调用者决定
            }
            else
            {
                // 否则实例化一个新对象
                obj = Instantiate(prefab, position, rotation);
                obj.name = key; // 确保名称一致，便于识别
            }

            return obj;
        }

        // 回收对象 - 将对象禁用并放回池中
        public void ReturnObject(GameObject obj)
        {
            if (obj == null) return;
            
            string key = obj.name;

            // 如果对象名称中包含"(Clone)"，去掉它
            if (key.Contains("(Clone)"))
            {
                key = key.Substring(0, key.IndexOf("(Clone)"));
            }

            // 如果这种类型的对象池不存在，创建一个
            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary[key] = new Queue<GameObject>();
            }

            // 禁用对象并放回池中
            obj.SetActive(false);
            // 将对象设为对象池的子对象，便于管理
            obj.transform.SetParent(transform);
            poolDictionary[key].Enqueue(obj);
        }
    }
}