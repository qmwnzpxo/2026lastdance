using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Upgrade/UpgradeDatabase")]
public class UpgradeDatabase : ScriptableObject
{
    [SerializeField] private List<UpgradeDefinition> _upgrades = new();

    private Dictionary<UpgradeStat, UpgradeDefinition> _map;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        _map = new Dictionary<UpgradeStat, UpgradeDefinition>();
        foreach (var def in _upgrades)
        {
            if (def == null) continue;
            if (_map.ContainsKey(def.stat))
            {
                Debug.LogWarning($"[UpgradeDatabase] 중복 stat: {def.name}[{def.stat}] 무시됨");
                continue;
            }
            _map[def.stat] = def;
        }
    }

    public UpgradeDefinition Get(UpgradeStat stat)
    {
        if (_map == null) BuildMap();
        _map.TryGetValue(stat, out var def);
        return def;
    }

    public IReadOnlyList<UpgradeDefinition> All => _upgrades;
}