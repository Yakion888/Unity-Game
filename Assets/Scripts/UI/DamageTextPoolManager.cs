using UnityEngine;
using System.Collections.Generic;

public class DamageTextPoolManager : MonoBehaviour
{
    public static DamageTextPoolManager Instance;

    [Header("配置")]
    public GameObject damageTextPrefab; // 漂字预制体
    public Transform canvasTransform;   // 必须是一个 World Space 的 Canvas，或者 Overlay Canvas
    public int initialPoolSize = 20;    // 初始准备20个

    private Queue<GameObject> textPool = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;

        // 提前生成备用，避免战斗中实例化
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

    // 呼出伤害数字的对外接口
    public void ShowDamageText(Vector3 spawnPosition, int damage, int damageType)
    {
        GameObject textObj;
        if (textPool.Count > 0)
            textObj = textPool.Dequeue();
        else
            textObj = CreateNewText();

        textObj.SetActive(true);
        textObj.transform.position = spawnPosition;
        
        // 调用单体的 Setup 方法初始化颜色和数字
        DamageTextItem item = textObj.GetComponent<DamageTextItem>();
        if (item != null)
        {
            item.Setup(damage, damageType);
        }
    }

    // 回收接口
    public void ReturnToPool(GameObject textObj)
    {
        textObj.SetActive(false);
        textObj.transform.SetParent(canvasTransform); // 确保还是画布的子层级
        textPool.Enqueue(textObj);
    }
}