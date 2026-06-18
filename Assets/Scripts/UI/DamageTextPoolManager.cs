using UnityEngine;
using System.Collections.Generic;

public class DamageTextPoolManager : MonoBehaviour
{
    public static DamageTextPoolManager Instance;

    [Header("配置")]
    public GameObject damageTextPrefab; // 漂字预制体
    public Transform canvasTransform;   // World Space / Overlay Canvas
    public int initialPoolSize = 20;    // 初始预生成数量

    // ───────────────────────────────────────
    // 【GC 优化】仅声明，不隐式实例化。
    // 隐式 new Queue<T>() 底层数组默认容量 = 0，
    // Awake 中循环 Enqueue 20 次会触发 0→4→8→16→32 共 4 次扩容，
    // 每次扩容抛弃旧数组 → 4 次 GC 尖峰。
    // 在 Awake 预热循环之前用 initialPoolSize 精准初始化，
    // 底层 T[20] 一次分配到位，零扩容。
    // ───────────────────────────────────────
    private Queue<GameObject> textPool;

    private void Awake()
    {
        Instance = this;

        // ── GC 优化：传入容量，底层数组一次分配到位 ──
        textPool = new Queue<GameObject>(initialPoolSize);

        // 预热：提前生成备用漂字，避免战斗中 Instantiate
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewText();
        }
    }

    private GameObject CreateNewText()
    {
        GameObject textObj = Instantiate(damageTextPrefab, canvasTransform);
        textObj.SetActive(false);
        textPool.Enqueue(textObj);
        return textObj;
    }

    /// <summary>
    /// 显示一个伤害漂字。池中有闲置则复用；池空则临时新建。
    /// </summary>
    public void ShowDamageText(Vector3 spawnPosition, int damage, int damageType)
    {
        GameObject textObj;
        if (textPool.Count > 0)
            textObj = textPool.Dequeue();
        else
            textObj = CreateNewText();

        textObj.SetActive(true);
        textObj.transform.position = spawnPosition;

        DamageTextItem item = textObj.GetComponent<DamageTextItem>();
        if (item != null)
        {
            item.Setup(damage, damageType);
        }
    }

    /// <summary>
    /// 回收漂字到池中（由 DamageTextItem 动画结束时调用）。
    /// </summary>
    public void ReturnToPool(GameObject textObj)
    {
        textObj.SetActive(false);
        textObj.transform.SetParent(canvasTransform);
        textPool.Enqueue(textObj);
    }
}
