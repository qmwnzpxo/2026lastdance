using System;
using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 패턴 선택 표 (boss.md 11장). 현민이 인스펙터에서 튜닝한다.
    /// 초기값 = 11장 표. 2페이즈 가중치는 〔임시값〕 (11장 주석 — 플레이 테스트로 조정).
    /// </summary>
    [CreateAssetMenu(menuName = "UnMachine/Config/Pattern Table", fileName = "PatternTableConfig")]
    public sealed class PatternTableConfig : ScriptableObject
    {
        [Header("거리 구간 (보스-플레이어 x 거리, Unit)")]
        [Tooltip("이 값 미만 = 근거리. spec: boss.md 11장 5, 17장 #8")]
        [SerializeField, Min(0f)] private float _nearMaxDistance = 5f;

        [Tooltip("이 값 이상 = 원거리 (사이는 중거리). spec: boss.md 11장 20, 17장 #8")]
        [SerializeField, Min(0f)] private float _farMinDistance = 20f;

        [Header("체류 보정 (근거리 → P1, 원거리 → P2)")]
        [Tooltip("같은 구간에 이 시간(초) 이상 머물면 보정. spec: boss.md 11장 3초")]
        [SerializeField, Min(0f)] private float _dwellBonusSeconds = 3f;

        [Tooltip("보정 배율. spec: boss.md 11장 ×2")]
        [SerializeField, Min(0f)] private float _dwellBonusMultiplier = 2f;

        [Header("1페이즈 후보 (spec: boss.md 11장)")]
        [SerializeField] private Entry[] _phase1Near =
        {
            new Entry(SelectionUnit.P3, 5f), new Entry(SelectionUnit.P1, 3f), new Entry(SelectionUnit.P2, 1f),
        };

        [SerializeField] private Entry[] _phase1Mid =
        {
            new Entry(SelectionUnit.P5, 4f), new Entry(SelectionUnit.P1, 3f), new Entry(SelectionUnit.P2, 3f),
        };

        [SerializeField] private Entry[] _phase1Far =
        {
            new Entry(SelectionUnit.P2, 5f), new Entry(SelectionUnit.P4, 3f), new Entry(SelectionUnit.P5, 2f),
        };

        [Header("2페이즈 후보 〔임시값: 2P 가중치〕 (spec: boss.md 11장)")]
        [SerializeField] private Entry[] _phase2Near =
        {
            new Entry(SelectionUnit.P3, 4f), new Entry(SelectionUnit.P1, 3f),
            new Entry(SelectionUnit.C1, 2f), new Entry(SelectionUnit.C3, 1f),
        };

        [SerializeField] private Entry[] _phase2Mid =
        {
            new Entry(SelectionUnit.P5, 3f), new Entry(SelectionUnit.P1, 2f), new Entry(SelectionUnit.P2, 2f),
            new Entry(SelectionUnit.C2, 2f), new Entry(SelectionUnit.P6, 1f),
        };

        [SerializeField] private Entry[] _phase2Far =
        {
            new Entry(SelectionUnit.P2, 3f), new Entry(SelectionUnit.P4, 3f),
            new Entry(SelectionUnit.C4, 2f), new Entry(SelectionUnit.P6, 2f),
        };

        /// <summary>근거리 상한 (Unit).</summary>
        public float NearMaxDistance => _nearMaxDistance;

        /// <summary>원거리 하한 (Unit).</summary>
        public float FarMinDistance => _farMinDistance;

        /// <summary>
        /// Core 선택 표를 만든다. 값이 잘못되면(빈 목록, 단위 중복, 가중치 전부 0) ArgumentException —
        /// 호출하는 어댑터가 GameLog.Error 후 기능을 끈다.
        /// </summary>
        public PatternTable ToTable()
        {
            return new PatternTable(
                ToWeights(_phase1Near),
                ToWeights(_phase1Mid),
                ToWeights(_phase1Far),
                ToWeights(_phase2Near),
                ToWeights(_phase2Mid),
                ToWeights(_phase2Far),
                _dwellBonusSeconds,
                _dwellBonusMultiplier);
        }

        /// <summary>이 표의 거리 경계로 구간 추적기를 만든다.</summary>
        public DistanceBandTracker CreateDistanceBandTracker()
        {
            return new DistanceBandTracker(_nearMaxDistance, _farMinDistance);
        }

        private void OnValidate()
        {
            // 원거리 하한이 근거리 상한보다 작으면 DistanceBandTracker 가 예외 → 인스펙터에서 바로 맞춘다.
            if (_farMinDistance < _nearMaxDistance)
            {
                _farMinDistance = _nearMaxDistance;
            }
        }

        private static PatternWeight[] ToWeights(Entry[] entries)
        {
            if (entries == null)
            {
                return Array.Empty<PatternWeight>();
            }

            var weights = new PatternWeight[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                weights[i] = entries[i].ToWeight();
            }

            return weights;
        }

        /// <summary>인스펙터용 표 한 칸 (단위 + 가중치).</summary>
        [Serializable]
        private struct Entry
        {
            [SerializeField] private SelectionUnit _unit;
            [SerializeField, Min(0f)] private float _weight;

            public Entry(SelectionUnit unit, float weight)
            {
                _unit = unit;
                _weight = weight;
            }

            public PatternWeight ToWeight()
            {
                return new PatternWeight(_unit, _weight);
            }
        }
    }
}
