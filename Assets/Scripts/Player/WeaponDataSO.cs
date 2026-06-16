using UnityEngine;

// 定义技能类型枚举
public enum WeaponSkillType
{
    None,
    WaveSlash,      // 裂地剑气
    QTEUltimate     // 终极连斩(QTE)
}

// 加上这行代码就可以在 Unity 里像创建 Material 一样创建武器数据包了
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "ARPG/武器数据包 (Weapon Data)")]
public class WeaponDataSO : ScriptableObject
{
    
    [Header("基本信息")]
    public string weaponName = "狼的末路";
    public float weaponBaseAttack = 40f; // 这把武器的初始攻击力
    public GameObject weaponModelPrefab; //武器的 3D 模型预制体

    // 这把武器附带的专属技能
    [Header("专属武器技能")]
    public WeaponSkillType exclusiveSkill = WeaponSkillType.WaveSlash;

    [Header("重攻击配置 (5段)")]
    public int[] heavyAttackDamage = new int[5] { 15, 20, 20, 35, 50 };
    public float[] heavyAttackKnockback = new float[5] { 5f, 6f, 7f, 8f, 10f };
    public float[] heavyAttackStaminaCost = new float[5] { 10f, 12f, 14f, 16f, 18f };
    public float heavyAttackRage = 10f;
    public GameObject[] heavyAttackEffects;
    public AudioClip[] attackSwingSounds;
    public AudioClip[] attackHitSounds;
    public AudioClip[] heavyAttackVoices;

    // 为每一段攻击配置不同的受击火花（比如第5下大爆点）
    public GameObject[] heavyAttackHitEffects;
    public Vector3[] heavyAttackVFXRotations = new Vector3[5] {
        new Vector3(20, -90, 245),
        new Vector3(90, 0, 5),
        new Vector3(20, -30, -45),
        new Vector3(90, 0, 0),
        new Vector3(80, -20, 0)
    }; 

    [Header("轻攻击配置 (3段)")]
    public int[] lightAttackDamage = new int[3] { 10, 12, 15 };
    public float[] lightAttackKnockback = new float[3] { 3f, 4f, 6f };
    public float[] lightAttackForwardOffset = new float[3] { 1.0f, 1.5f, 1.5f };
    public float[] lightAttackRadius = new float[3] { 0.5f, 0.8f, 0.8f };
    public float[] lightAttackAngle = new float[3] { 60f, 60f, 90f };
    public float lightAttackRage = 3f;
    public GameObject[] lightAttackEffects;
    public AudioClip[] lightAttackSwingSounds;
    public AudioClip[] lightAttackHitSounds;
    public AudioClip[] lightAttackVoices;

    [Header("滑行攻击配置")]
    public int runningAttackDamage = 50;
    public float runningAttackKnockback = 8f;
    public float runningAttackStaminaCost = 25f;
    public float runningAttackRage = 15f;
    public GameObject runningAttackEffect;
    public AudioClip slidingWhooshSound;
    public AudioClip[] runningAttackVoices;
}