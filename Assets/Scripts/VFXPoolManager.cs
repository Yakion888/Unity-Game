using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXPoolManager : MonoBehaviour
{
    // 单例模式，方便全局随时取用特效
    public static VFXPoolManager Instance;

    // 核心对象池字典：键是特效的名字，值是存放该特效的队列
    private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 从池子中获取特效（代替 Instantiate）
    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        // 如果字典里还没有这个特效的队列，建一个
        if (!poolDictionary.ContainsKey(key))
        {
            poolDictionary[key] = new Queue<GameObject>();
        }

        GameObject objToSpawn = null;

        // 如果池子里有闲置的特效，直接拿出来用
        if (poolDictionary[key].Count > 0)
        {
            objToSpawn = poolDictionary[key].Dequeue();
            objToSpawn.transform.position = position;
            objToSpawn.transform.rotation = rotation;

            // 防止特效留在池子节点或上一个怪物节点下跟着移动
            objToSpawn.transform.SetParent(null); 

            objToSpawn.SetActive(true);
        }
        // 如果池子空了（或者第一次请求），才实例化一个新的
        else
        {
            objToSpawn = Instantiate(prefab, position, rotation);
            objToSpawn.name = key; // 抹除 "(Clone)" 后缀，保持名字干净
            objToSpawn.transform.SetParent(null); //新生成的特效也直接放到世界根目录
        }

        return objToSpawn;
    }

    // 将特效收回池子中（代替 Destroy）
    public void ReturnToPool(GameObject obj, float delay)
    {
        StartCoroutine(ReturnRoutine(obj, delay));
    }

    private IEnumerator ReturnRoutine(GameObject obj, float delay)
    {
        // 延迟一段时间后回收
        yield return new WaitForSeconds(delay);

        // 防御性编程：如果物体已经被销毁，直接退出
        if (obj == null) yield break;

        obj.SetActive(false); // 隐藏特效
        obj.transform.SetParent(transform); // 从怪物身上解绑，收回到管理器节点下

        if (!poolDictionary.ContainsKey(obj.name))
        {
            poolDictionary[obj.name] = new Queue<GameObject>();
        }

        // 【安全锁】：检查队列里是否已经存在这个物体了！
        // 防止出现同一个物体被双重回收（Double Free）导致的池子崩溃和特效穿梭 Bug
        if (!poolDictionary[obj.name].Contains(obj))
        {
            poolDictionary[obj.name].Enqueue(obj);
        }
    }
}