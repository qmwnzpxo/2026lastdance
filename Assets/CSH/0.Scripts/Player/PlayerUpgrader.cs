using UnityEngine;

public class PlayerUpgrader : MonoBehaviour
{
    [SerializeField] private PlayerState _playerState;
    public void UpgradeMaxHP(float amount, float shard)
    {
        _playerState.MaxHP += amount;
        _playerState.HP += amount; // 최대 HP 증가 시 현재 HP도 같이 증가        
    }

    public void UpgradeStamina(float amount, float shard)
    {
        _playerState.maxStamina += amount;        

    }

    public void UpgradeMaxGauge(float amount, float shard)
    {
        _playerState.MaxDashGauge += amount;
        TempSaveManager.Instance.UsedShard(shard);
    }

    public void UpgradeDamage(float amount, float shard)
    {
        _playerState.damage += amount;
    }
}