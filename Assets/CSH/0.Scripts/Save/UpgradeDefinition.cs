using UnityEngine;

/// <summary>
/// 업그레이드 항목 1개를 정의하는 ScriptableObject
/// 사용법
/// 1. Create → Upgrade → UpgradeDefinition 으로 생성
/// 2. UpgradeStat 열거형에서 stat 선택
/// 3. amountPerLevel과 maxLevel 설정 (amountPerLevel=10, maxLevel=3이면 최대 레벨은 3이고, 각 레벨마다 10씩 증가)
/// </summary>
[CreateAssetMenu(fileName = "UpgradeDef_", menuName = "Upgrade/UpgradeDefinition")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("기본 정보")]
    public UpgradeStat stat;
    public string displayName;
    [TextArea] public string description;

    [Header("수치")]
    public float amountPerLevel = 1f; // 레벨당 증가량
    public int maxLevel = 5;

    [Header("비용")]
    public float[] shardCostPerLevel;

    // ── 유틸 ──────────────────────────────────────────────────
    public float GetCost(int currentLevel)
    {
        if (currentLevel >= maxLevel) return -1f;
        if (shardCostPerLevel == null || currentLevel >= shardCostPerLevel.Length) return 0f;
        return shardCostPerLevel[currentLevel];
    }

    // 현재 레벨까지 누적된 총 스탯 증가량
    public float GetTotalAmount(int level)
        => amountPerLevel * level;
}