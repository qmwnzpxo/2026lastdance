using UnityEngine;
public enum UpgradeStat
{
    MaxHP, MaxStamina, MaxDashGauge,
    RegenStamina, DashGaugeGain,
    MoveSpeed, RunSpeed, JumpForce,
    Damage, AttackSpeed, AttackBox, DashBox,

    Length,    // 업그레이드 가능한 스탯 개수 (배열 크기용)
}
public class PlayerUpgrader : MonoBehaviour
{
    [SerializeField] private UpgradeDatabase database;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerDash playerDash;

    // ────────────────────────────────────────────────────────
    #region 초기화
    private void Awake()
    {
        if (playerState == null) playerState = FindFirstObjectByType<PlayerState>();
        if (playerMove == null) playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat>();
        if (playerDash == null) playerDash = FindFirstObjectByType<PlayerDash>();
        if(database == null) database = FindFirstObjectByType<UpgradeDatabase>();
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region 외부 API
    public bool TryUpgrade(UpgradeStat stat)
    {
        var def = database.Get(stat);
        if (def == null)
        {
            Debug.LogWarning($"[PlayerUpgrader] DB에 {stat} 없음");
            return false;
        }

        SaveData data = SaveManager.Instance.Load();
        int statIndex = (int)stat;
        int currentLevel = data.upgradeLevels[statIndex];

        if (currentLevel >= def.maxLevel)
        {
            return false;
        }

        float cost = def.GetCost(currentLevel);
        if (data.RemainShard < cost)
        {
            return false;
        }

        data.upgradeLevels[statIndex]++;
        data.usedShard += cost;

        SaveManager.Instance.Save(data);
        ApplyStat(stat, data.upgradeLevels[statIndex], def, data);

        return true;
    }

    public void LoadAndApply()
    {
        SaveData data = SaveManager.Instance.Load();
        ApplyAll(data);
    }

    public int GetLevel(UpgradeStat stat)
    {
        SaveData data = SaveManager.Instance.Load();
        return data.upgradeLevels[(int)stat];
    }

    public float GetNextCost(UpgradeStat stat)
    {
        var def = database.Get(stat);
        if (def == null) return -1f;
        int level = GetLevel(stat);
        return def.GetCost(level);
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region 내부

    // SaveData 기반으로 플레이어한테 스탯 반영
    private void ApplyAll(SaveData data)
    {
        foreach (UpgradeStat stat in System.Enum.GetValues(typeof(UpgradeStat)))
        {
            var def = database.Get(stat);
            if (def == null) continue;
            int level = data.upgradeLevels[(int)stat];
            ApplyStat(stat, level, def, data);
        }
    }

    private void ApplyStat(UpgradeStat stat, int level, UpgradeDefinition def, SaveData data)
    {
        float total = def.GetTotalAmount(level);

        switch (stat)
        {
            case UpgradeStat.MaxHP:
                playerState.MaxHP = data.baseMaxHP + total;
                playerState.HP = Mathf.Min(playerState.HP, playerState.MaxHP);
                break;
            case UpgradeStat.MaxStamina:
                playerState.maxStamina = data.baseMaxStamina + total;
                break;
            case UpgradeStat.MaxDashGauge:
                playerState.MaxDashGauge = data.baseMaxDashGauge + total;
                break;
            case UpgradeStat.DashGaugeGain:
                playerState.dashGaugePerAttack = data.baseDashGaugeGain + total;
                break;
            case UpgradeStat.MoveSpeed:
                    playerMove.MoveSpeed = data.baseMoveSpeed + total;
                break;
            case UpgradeStat.RunSpeed:
                    playerMove.RunSpeed = data.baseRunSpeed + total;
                break;
            case UpgradeStat.JumpForce:
                    playerMove.JumpForce = data.baseJumpForce + total;
                break;
            case UpgradeStat.DashBox:
                    playerDash.DashAttackBoxSize = data.baseDashBox + total;
                break;
            case UpgradeStat.Damage:
                playerState.damage = data.baseDamage + total;
                break;
            case UpgradeStat.AttackSpeed:
                playerCombat.AttackSpeed = data.baseAttackSpeed + total;
                break;
            case UpgradeStat.AttackBox:
                playerCombat.AttackBoxSize = data.baseAttackBox + total;
                break;

        }
    }
    #endregion

}