using System.Collections.Generic;
using UnityEngine;
using BossSystem;

// ═══════════════════════════════════════════════════════════════════════
// ItemDropOnBreak.cs
//
// BreakableObject를 상속받아, 파괴 시 아이템(회복/칩/섬광탄)을 드랍한다.
// 드랍 테이블(_dropEntries)에 등록된 항목만 가중치 기반으로 랜덤 드랍되므로,
// 특정 오브젝트가 일부 종류만 드랍하게 하려면 해당 항목만 등록하면 된다.
//
// 칩 드랍 규칙:
//   - 플레이어가 아직 보유하지 않은 칩 중 랜덤으로 선택
//   - 모든 칩을 이미 보유한 상태라면, 칩 항목을 제외하고 남은 항목들로 재추첨
//   - 재추첨할 항목도 없으면 드랍하지 않음(경고 로그)
// ═══════════════════════════════════════════════════════════════════════
namespace ItemSystem
{
    public enum ItemDropType
    {
        Heal,
        Chip,
        Flashbang
    }

    [System.Serializable]
    public class ItemDropEntry
    {
        public ItemDropType type;

        [Tooltip("Heal / Flashbang 타입 전용. Chip 타입은 무시되고 _chipPrefabs에서 별도 선택됨")]
        public GameObject prefab;

        [Min(0f)]
        public float weight = 1f;
    }

    [System.Serializable]
    public class ChipPrefabEntry
    {
        public PlayerChip chip;
        public GameObject prefab;
    }

    public class ItemDropOnBreak : BreakableObject
    {
        [Header("Drop Table")]
        [Tooltip("이 오브젝트가 드랍 가능한 아이템 목록. 등록된 항목 중에서만 가중치 추첨됨")]
        [SerializeField] private ItemDropEntry[] _dropEntries;

        [Header("Chip Prefabs (색상별)")]
        [SerializeField] private ChipPrefabEntry[] _chipPrefabs;

        [Header("Drop Position")]
        [SerializeField] private Vector2 _dropOffset = Vector2.zero;

        // ─────────────────────────────────────────────────────────────────
        // 파괴 처리
        // ─────────────────────────────────────────────────────────────────

        protected override void OnBroken()
        {
            SpawnDrop();
            gameObject.SetActive(false);
        }

        private void SpawnDrop()
        {
            if (_dropEntries == null || _dropEntries.Length == 0) return;

            var entry = PickWeightedEntry(_dropEntries, excludeType: null);
            if (entry == null) return;

            if (entry.type == ItemDropType.Chip)
                SpawnChip();
            else
                SpawnPrefab(entry.prefab);
        }

        // ── 칩 드랍 ──────────────────────────────────────────────────────

        private void SpawnChip()
        {
            var playerState = FindPlayerState();
            if (playerState == null)
            {
                Debug.LogWarning("[ItemDropOnBreak] PlayerState를 찾을 수 없어 칩 드랍을 취소합니다.");
                return;
            }

            var missingChips = GetMissingChips(playerState);

            if (missingChips.Count == 0)
            {
                // 모든 칩 보유 중 → 칩 항목 제외하고 재추첨
                var fallbackEntry = PickWeightedEntry(_dropEntries, excludeType: ItemDropType.Chip);
                if (fallbackEntry == null)
                {
                    Debug.LogWarning("[ItemDropOnBreak] 칩 드랍 대체 항목이 없습니다. 드랍을 취소합니다.");
                    return;
                }
                SpawnPrefab(fallbackEntry.prefab);
                return;
            }

            PlayerChip chosenChip = missingChips[Random.Range(0, missingChips.Count)];
            GameObject chipPrefab = GetChipPrefab(chosenChip);

            if (chipPrefab == null)
            {
                Debug.LogWarning($"[ItemDropOnBreak] {chosenChip} 색상에 대응하는 칩 프리팹이 등록되지 않았습니다.");
                return;
            }

            GameObject go = ObjectPool.Get(chipPrefab, GetDropPosition(), Quaternion.identity);
            var chipItem = go != null ? go.GetComponent<ChipItemPickup>() : null;

            if (chipItem == null)
            {
                Debug.LogWarning("[ItemDropOnBreak] 칩 프리팹에 ChipItemPickup 컴포넌트가 없습니다.");
                return;
            }

            chipItem.Init(chosenChip);
        }

        private List<PlayerChip> GetMissingChips(PlayerState playerState)
        {
            var all = (PlayerChip[])System.Enum.GetValues(typeof(PlayerChip));
            var missing = new List<PlayerChip>();

            foreach (var chip in all)
            {
                if (!playerState.ownedChips.Contains(chip))
                    missing.Add(chip);
            }
            return missing;
        }

        private GameObject GetChipPrefab(PlayerChip chip)
        {
            if (_chipPrefabs == null) return null;

            foreach (var entry in _chipPrefabs)
                if (entry != null && entry.chip == chip) return entry.prefab;

            return null;
        }

        // ── 공용 스폰 ────────────────────────────────────────────────────

        private void SpawnPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            ObjectPool.Get(prefab, GetDropPosition(), Quaternion.identity);
        }

        private Vector3 GetDropPosition() => transform.position + (Vector3)_dropOffset;

        // ── 가중치 추첨 ──────────────────────────────────────────────────

        private ItemDropEntry PickWeightedEntry(ItemDropEntry[] entries, ItemDropType? excludeType)
        {
            float total = 0f;

            foreach (var e in entries)
            {
                if (!IsValidEntry(e, excludeType)) continue;
                total += e.weight;
            }

            if (total <= 0f) return null;

            float r = Random.Range(0f, total);
            float cumulative = 0f;

            foreach (var e in entries)
            {
                if (!IsValidEntry(e, excludeType)) continue;

                cumulative += e.weight;
                if (r <= cumulative) return e;
            }

            return null;
        }

        private bool IsValidEntry(ItemDropEntry e, ItemDropType? excludeType)
        {
            if (e == null || e.weight <= 0f) return false;
            if (excludeType.HasValue && e.type == excludeType.Value) return false;
            if (e.type != ItemDropType.Chip && e.prefab == null) return false;
            return true;
        }

        private PlayerState FindPlayerState()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            return playerObj != null ? playerObj.GetComponent<PlayerState>() : null;
        }
    }
}