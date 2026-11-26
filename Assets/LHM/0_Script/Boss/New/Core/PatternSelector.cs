using System;
using System.Collections.Generic;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 다음 선택 단위를 고른다 (boss.md 11장). 순서: 표 가중치 → 체류 보정 → 사용 불가 대체 → 반복 방지 → 가중치 추첨.
    /// 난수는 주입된 IRandom 만 쓴다 — 같은 시드·같은 입력이면 같은 선택 순서.
    /// </summary>
    /// <remarks>
    /// 반복 방지는 <b>대체 후</b> 단위로 센다 (P6 → P5 대체가 끼어도 같은 단위 3연속이 나오지 않게).
    /// 후보가 반복 금지 단위 하나뿐이면 멈추지 않도록 반복을 허용한다 (LastRepeatBanRelaxed).
    /// </remarks>
    public sealed class PatternSelector
    {
        private const int HistoryLength = 2;

        private readonly PatternTable _table;
        private readonly IRandom _random;
        private readonly Func<SelectionUnit, bool> _isAvailable;

        // 마지막 TrySelect 의 최종 후보 (대체·보정 반영). _sources[i] = 그 후보의 표 원본 단위.
        private readonly List<PatternWeight> _candidates = new List<PatternWeight>();
        private readonly List<SelectionUnit> _sources = new List<SelectionUnit>();

        private SelectionUnit _last;
        private SelectionUnit _previous;
        private int _historyCount;

        /// <summary>선택기를 만든다.</summary>
        /// <param name="table">후보 가중치 표.</param>
        /// <param name="random">난수 공급자 (시드 고정 시 재현 가능).</param>
        /// <param name="isAvailable">단위 사용 가능 여부 (예: 드론이 살아 있으면 P6 불가). null 이면 전부 가능.
        /// 생성 시 한 번만 받는다 — 매 선택마다 대리자를 만들지 않게.</param>
        public PatternSelector(PatternTable table, IRandom random, Func<SelectionUnit, bool> isAvailable = null)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _isAvailable = isAvailable;
        }

        /// <summary>마지막 선택의 최종 후보와 가중치 (디버그 표시용). 대체로 같은 단위가 두 번 나올 수 있다.</summary>
        public IReadOnlyList<PatternWeight> LastCandidates => _candidates;

        /// <summary>마지막 선택에서 체류 보정(×배율)이 실제로 붙었는지.</summary>
        public bool LastDwellBonusApplied { get; private set; }

        /// <summary>마지막 선택 결과가 사용 불가 단위를 대체한 것인지 (예: P6 → P5).</summary>
        public bool LastWasSubstituted { get; private set; }

        /// <summary>마지막 선택에서 대체 전 원본 단위. LastWasSubstituted 가 false 면 선택 단위와 같다.</summary>
        public SelectionUnit LastSourceUnit { get; private set; }

        /// <summary>후보가 반복 금지 단위뿐이라 반복 방지를 풀고 골랐는지.</summary>
        public bool LastRepeatBanRelaxed { get; private set; }

        /// <summary>기록된 최근 선택 수 (0~2).</summary>
        public int HistoryCount => _historyCount;

        /// <summary>가장 최근 선택. HistoryCount ≥ 1 일 때만 의미 있음.</summary>
        public SelectionUnit LastSelected => _last;

        /// <summary>그 전 선택. HistoryCount = 2 일 때만 의미 있음.</summary>
        public SelectionUnit PreviousSelected => _previous;

        /// <summary>다음 선택에서 제외될 단위가 있으면 true (같은 단위 2연속). spec: boss.md 11장 반복 방지</summary>
        public bool TryGetBannedUnit(out SelectionUnit unit)
        {
            unit = _last;
            return _historyCount >= HistoryLength && _last == _previous;
        }

        /// <summary>다음 단위를 고르고 기록한다.</summary>
        /// <param name="phase">현재 페이즈 (1 또는 2).</param>
        /// <param name="band">플레이어와의 거리 구간.</param>
        /// <param name="dwellSeconds">그 구간 체류 시간(초). 0 이상 유한값.</param>
        /// <param name="unit">고른 단위.</param>
        /// <returns>고를 후보가 하나도 없으면 false (전부 사용 불가). 기록은 바뀌지 않는다.</returns>
        public bool TrySelect(int phase, DistanceBand band, float dwellSeconds, out SelectionUnit unit)
        {
            float dwell = BossFlowMachine.RequireNonNegativeFinite(dwellSeconds, nameof(dwellSeconds));
            IReadOnlyList<PatternWeight> weights = _table.GetWeights(phase, band);
            bool isDwellLong = dwell >= _table.DwellBonusSeconds;
            bool hasBan = TryGetBannedUnit(out SelectionUnit banned);

            LastDwellBonusApplied = BuildCandidates(weights, band, isDwellLong, hasBan, banned);
            LastRepeatBanRelaxed = false;

            if (_candidates.Count == 0 && hasBan)
            {
                LastDwellBonusApplied = BuildCandidates(weights, band, isDwellLong, false, banned);
                LastRepeatBanRelaxed = _candidates.Count > 0;
            }

            if (_candidates.Count == 0)
            {
                unit = default;
                LastWasSubstituted = false;
                LastSourceUnit = default;
                return false;
            }

            int index = PickIndex();
            unit = _candidates[index].Unit;
            LastSourceUnit = _sources[index];
            LastWasSubstituted = LastSourceUnit != unit;
            Record(unit);
            return true;
        }

        /// <summary>최근 선택 기록을 지운다 (새 전투 시작).</summary>
        public void ResetHistory()
        {
            _historyCount = 0;
            _last = default;
            _previous = default;
        }

        /// <summary>
        /// 체류 보정을 받는 단위: 그 구간을 벗어나게 만드는 패턴. spec: boss.md 11장 (근거리 → P1, 원거리 → P2)
        /// </summary>
        public static bool TryGetDwellEscapeUnit(DistanceBand band, out SelectionUnit unit)
        {
            switch (band)
            {
                case DistanceBand.Near:
                    unit = SelectionUnit.P1;
                    return true;
                case DistanceBand.Far:
                    unit = SelectionUnit.P2;
                    return true;
                default:
                    unit = default;
                    return false;
            }
        }

        /// <summary>사용 불가일 때 대신 쓸 단위. spec: boss.md 6장 P6 (드론 상한 → P5 대체)</summary>
        public static bool TryGetSubstitute(SelectionUnit unit, out SelectionUnit substitute)
        {
            if (unit == SelectionUnit.P6)
            {
                substitute = SelectionUnit.P5;
                return true;
            }

            substitute = default;
            return false;
        }

        // 후보를 _candidates/_sources 에 채운다. 체류 보정이 실제로 붙었으면 true.
        private bool BuildCandidates(
            IReadOnlyList<PatternWeight> weights,
            DistanceBand band,
            bool isDwellLong,
            bool hasBan,
            SelectionUnit banned)
        {
            _candidates.Clear();
            _sources.Clear();

            bool hasEscapeUnit = TryGetDwellEscapeUnit(band, out SelectionUnit escapeUnit);
            bool isBonusApplied = false;

            for (int i = 0; i < weights.Count; i++)
            {
                PatternWeight entry = weights[i];
                if (!TryResolve(entry.Unit, out SelectionUnit resolved) || (hasBan && resolved == banned))
                {
                    continue;
                }

                float weight = entry.Weight;
                if (isDwellLong && hasEscapeUnit && entry.Unit == escapeUnit && weight > 0f)
                {
                    weight *= _table.DwellBonusMultiplier;
                    isBonusApplied = true;
                }

                if (weight <= 0f)
                {
                    continue;
                }

                _candidates.Add(new PatternWeight(resolved, weight));
                _sources.Add(entry.Unit);
            }

            return isBonusApplied;
        }

        private bool TryResolve(SelectionUnit unit, out SelectionUnit resolved)
        {
            if (IsAvailable(unit))
            {
                resolved = unit;
                return true;
            }

            if (TryGetSubstitute(unit, out SelectionUnit substitute) && IsAvailable(substitute))
            {
                resolved = substitute;
                return true;
            }

            resolved = default;
            return false;
        }

        private bool IsAvailable(SelectionUnit unit)
        {
            return _isAvailable == null || _isAvailable(unit);
        }

        private int PickIndex()
        {
            float total = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                total += _candidates[i].Weight;
            }

            // Value01 은 [0, 1) → roll 은 [0, total). 부동소수 오차로 끝까지 가면 마지막 후보.
            float roll = _random.Value01 * total;
            float cumulative = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                cumulative += _candidates[i].Weight;
                if (roll < cumulative)
                {
                    return i;
                }
            }

            return _candidates.Count - 1;
        }

        private void Record(SelectionUnit unit)
        {
            _previous = _last;
            _last = unit;
            if (_historyCount < HistoryLength)
            {
                _historyCount++;
            }
        }
    }
}
