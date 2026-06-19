using UnityEngine;
using UnityEngine.AddressableAssets;

// 定义技能类型枚举
public enum WeaponSkillType
{
    None,
    WaveSlash,      // 裂地剑气
    QTEUltimate     // 终极连斩(QTE)
}

/// <summary>
/// 武器数据 ScriptableObject —— Addressables 版
///
/// ═══════════════════════════════════════════════════════════
/// 【重构说明】
///   旧版：直接 public GameObject weaponModelPrefab、AudioClip[] 等，
///        加载任意 SO 即把全部资源拉入内存。
///   新版：所有资产字段改为 AssetReference / AssetReferenceT，
///        SO 本身只存地址字符串，零内存占用。
///        实际加载由 WeaponRuntimeAssets 在 EquipWeaponAsync 中完成。
/// ═══════════════════════════════════════════════════════════
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "ARPG/武器数据包 (Weapon Data)")]
public class WeaponDataSO : ScriptableObject
{
    [Header("基本信息")]
    public string weaponName = "狼的末路";
    public float weaponBaseAttack = 40f;

    /// <summary>武器 3D 模型预制体的 Addressables 引用</summary>
    public AssetReferenceGameObject weaponModelRef;

    /// <summary>这把武器附带的专属技能</summary>
    [Header("专属武器技能")]
    public WeaponSkillType exclusiveSkill = WeaponSkillType.WaveSlash;

    // ══════════════════════════════════════════════════════
    // 数值配置（纯数据，不涉及资源引用，保持原样）
    // ══════════════════════════════════════════════════════

    [Header("重攻击配置 (5段)")]
    public int[] heavyAttackDamage = new int[5] { 15, 20, 20, 35, 50 };
    public float[] heavyAttackKnockback = new float[5] { 5f, 6f, 7f, 8f, 10f };
    public float[] heavyAttackStaminaCost = new float[5] { 10f, 12f, 14f, 16f, 18f };
    public float heavyAttackRage = 10f;

    [Header("重攻击 — Addressables 资源引用")]
    public AssetReferenceGameObject[] heavyAttackEffectRefs;
    public AssetReferenceT<AudioClip>[] heavySwingSoundRefs;
    public AssetReferenceT<AudioClip>[] heavyHitSoundRefs;
    public AssetReferenceT<AudioClip>[] heavyVoiceRefs;
    public AssetReferenceGameObject[] heavyHitEffectRefs;
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

    [Header("轻攻击 — Addressables 资源引用")]
    public AssetReferenceGameObject[] lightAttackEffectRefs;
    public AssetReferenceT<AudioClip>[] lightSwingSoundRefs;
    public AssetReferenceT<AudioClip>[] lightHitSoundRefs;
    public AssetReferenceT<AudioClip>[] lightVoiceRefs;

    [Header("滑行攻击配置")]
    public int runningAttackDamage = 50;
    public float runningAttackKnockback = 8f;
    public float runningAttackStaminaCost = 25f;
    public float runningAttackRage = 15f;

    [Header("滑行攻击 — Addressables 资源引用")]
    public AssetReferenceGameObject runningAttackEffectRef;
    public AssetReferenceT<AudioClip> slidingWhooshSoundRef;
    public AssetReferenceT<AudioClip>[] runningVoiceRefs;
}
