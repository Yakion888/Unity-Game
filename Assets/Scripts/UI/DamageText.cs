using UnityEngine;
using TMPro; // 必须引入 TextMeshPro 命名空间

public class DamageText : MonoBehaviour
{
    public TextMeshPro textMesh;     // 3D 文本组件
    public float moveSpeed = 1.5f;   // 向上漂浮的速度
    public float fadeSpeed = 1.5f;   // 消失的速度
    public float lifetime = 1.5f;    // 存活时间

    private Color textColor;

    // 【核心修改】：把 bool isCrit 改成了 int damageType (0=普通, 1=暴击, 2=技能)
    public void Setup(int damageAmount, int damageType)
    {
        // 默认显示数字
        textMesh.text = damageAmount.ToString();

        if (damageType == 1) // 💥 1: 暴击伤害 (金色)
        {
            textMesh.color = new Color(1f, 0.8f, 0f, 1f); 
            textMesh.fontSize = 8;
            moveSpeed = 2.5f; 
        }
        else if (damageType == 2) // ✨ 2: 技能伤害 (紫色 + 感叹号)
        {
            // 紫色 (R:0.8, G:0.2, B:1.0) 
            textMesh.color = new Color(0.8f, 0.2f, 1f, 1f); 
            textMesh.fontSize = 10;     // 技能字体最大！
            moveSpeed = 1.0f;           // 技能伤害飘得慢一点，显得沉重有力
            lifetime = 2.0f;            // 存活时间更长
            textMesh.text = damageAmount.ToString() + "!"; // 技能数字后面加个霸气的感叹号
        }
        else // ⚔️ 0: 普通伤害 (白色)
        {
            textMesh.color = Color.white;
            textMesh.fontSize = 5;
            moveSpeed = 1.5f;
        }

        textColor = textMesh.color;

        // 随机偏移防重叠
        transform.position += new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), Random.Range(-0.3f, 0.3f));

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 向上漂浮
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 始终面向主相机（像向日葵一样看着玩家）
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        // 逐渐变透明 (Fade out)
        textColor.a -= fadeSpeed * Time.deltaTime;
        textMesh.color = textColor;
    }
}