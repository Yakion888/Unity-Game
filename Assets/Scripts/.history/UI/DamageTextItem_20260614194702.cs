using UnityEngine;
using TMPro;

public class DamageTextItem : MonoBehaviour
{
    public TextMeshProUGUI damageText;
    public float floatSpeed = 2f;      // 向上漂浮的速度
    public float lifetime = 1.0f;      // 存活时间

    private float timer;
    private Color originalColor;

    public void Setup(int damage, int damageType)
    {
        if (damageText == null) damageText = GetComponent<TextMeshProUGUI>();
        
        damageText.text = damage.ToString();
        timer = lifetime;

        // 根据伤害类型变色（0=白字普攻，1=金字暴击，2=紫字技能）
        switch (damageType)
        {
            case 0: originalColor = Color.white; break;
            case 1: originalColor = Color.yellow; break;
            case 2: originalColor = new Color(0.7f, 0.3f, 1f); break; // 紫色
            default: originalColor = Color.white; break;
        }
        damageText.color = originalColor;

        // 随机给一个微小的初始偏移，防止多个数字完全重叠
        transform.localPosition += new Vector3(Random.Range(-20f, 20f), Random.Range(-20f, 20f), 0);
    }

    private void Update()
    {
        // 向上漂浮
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 透明度淡出
        timer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(timer / (lifetime * 0.5f)); // 后半段开始变透明
        damageText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

        // 生命结束，回收到池子（替代 Destroy）
        if (timer <= 0)
        {
            DamageTextPoolManager.Instance.ReturnToPool(this.gameObject);
        }
    }
}