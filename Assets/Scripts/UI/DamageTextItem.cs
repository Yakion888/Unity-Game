using UnityEngine;
using TMPro;

public class DamageTextItem : MonoBehaviour
{
    public TextMeshProUGUI damageText;
    public float floatSpeed = 1.5f;      // 向上漂浮的速度
    public float lifetime = 1.0f;      // 存活时间

    private float timer;
    private Color originalColor;
    private Camera mainCam;

    public void Setup(int damage, int damageType)
    {
        if (damageText == null) damageText = GetComponentInChildren<TextMeshProUGUI>();
        if (damageText == null) return;
        
        // 缓存主摄像机，用于后续的朝向计算
        if (mainCam == null) mainCam = Camera.main; 
        
        damageText.text = damage.ToString();
        timer = lifetime;

        // 根据伤害类型变色（0=白字，1=金字暴击，2=紫字技能）
        switch (damageType)
        {
            case 0: originalColor = Color.white; break;
            case 1: originalColor = Color.yellow; break;
            case 2: originalColor = new Color(0.7f, 0.3f, 1f); break; 
            default: originalColor = Color.white; break;
        }
        damageText.color = originalColor;

        // 🛡️ 核心修复 1：在【世界坐标】下进行微小的随机偏移，防止被 Canvas 比例放大
        transform.position += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.5f), 0);
    }

    // 🛡️ 核心修复 2：使用 LateUpdate 确保在相机移动后，UI 再进行旋转对齐
    private void LateUpdate()
    {
        // 1. 【公告板效应 Billboard】：让 UI 永远平行面向摄像机的屏幕！
        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward; 
        }

        // 2. 向上漂浮
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 3. 透明度淡出
        timer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(timer / (lifetime * 0.5f)); 
        damageText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

        // 4. 生命结束，回收到池子
        if (timer <= 0)
        {
            DamageTextPoolManager.Instance.ReturnToPool(this.gameObject);
        }
    }
}