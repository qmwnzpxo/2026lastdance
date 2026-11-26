using System;
using System.Collections.Generic;

namespace UnMachine.Core.Boss
{
    /// <summary>
    /// 페이즈 × 거리 구간별 후보 가중치와 체류 보정 수치 (boss.md 11장 표).
    /// 어댑터가 PatternTableConfig(SO).ToTable() 로 만든다 (Core 는 SO 를 모른다). 만든 뒤 바뀌지 않는다.
    /// </summary>
    public sealed class PatternTable
    {
        /// <summary>첫 페이즈 번호.</summary>
        public const int FirstPhase = 1;

        /// <summary>마지막 페이즈 번호. spec: boss.md 8장 (2페이즈 구성)</summary>
        public const int LastPhase = 2;

        private const int BandCount = 3;

        // [페이즈 - 1][구간] → 가중치 목록. 생성 시 복사해서 외부 배열 변경에 영향받지 않는다.
        private readonly PatternWeight[][][] _weights;

        /// <summary>표를 만든다. 각 목록은 가중치 &gt; 0 인 단위가 하나 이상, 같은 단위 중복 없음.</summary>
        /// <param name="dwellBonusSeconds">체류 보정이 붙는 체류 시간(초). spec: boss.md 11장 3초</param>
        /// <param name="dwellBonusMultiplier">체류 보정 배율. spec: boss.md 11장 ×2</param>
        public PatternTable(
            PatternWeight[] phase1Near,
            PatternWeight[] phase1Mid,
            PatternWeight[] phase1Far,
            PatternWeight[] phase2Near,
            PatternWeight[] phase2Mid,
            PatternWeight[] phase2Far,
            float dwellBonusSeconds,
            float dwellBonusMultiplier)
        {
            DwellBonusSeconds = BossFlowMachine.RequireNonNegativeFinite(dwellBonusSeconds, nameof(dwellBonusSeconds));
            DwellBonusMultiplier =
                BossFlowMachine.RequireNonNegativeFinite(dwellBonusMultiplier, nameof(dwellBonusMultiplier));

            _weights = new[]
            {
                new[]
                {
                    CopyValidated(phase1Near, nameof(phase1Near)),
                    CopyValidated(phase1Mid, nameof(phase1Mid)),
                    CopyValidated(phase1Far, nameof(phase1Far)),
                },
                new[]
                {
                    CopyValidated(phase2Near, nameof(phase2Near)),
                    CopyValidated(phase2Mid, nameof(phase2Mid)),
                    CopyValidated(phase2Far, nameof(phase2Far)),
                },
            };
        }

        /// <summary>체류 보정이 붙는 체류 시간(초). 이 값 이상 머물면 보정.</summary>
        public float DwellBonusSeconds { get; }

        /// <summary>체류 보정 배율.</summary>
        public float DwellBonusMultiplier { get; }

        /// <summary>페이즈·구간의 후보 가중치 목록 (표 원본 값, 보정 전).</summary>
        /// <param name="phase">1 또는 2.</param>
        public IReadOnlyList<PatternWeight> GetWeights(int phase, DistanceBand band)
        {
            if (phase < FirstPhase || phase > LastPhase)
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, $"{FirstPhase}~{LastPhase} 이어야 한다.");
            }

            int bandIndex = (int)band;
            if (bandIndex < 0 || bandIndex >= BandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(band), band, "알 수 없는 거리 구간.");
            }

            return _weights[phase - FirstPhase][bandIndex];
        }

        private static PatternWeight[] CopyValidated(PatternWeight[] source, string paramName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(paramName);
            }

            bool hasPositive = false;
            for (int i = 0; i < source.Length; i++)
            {
                hasPositive |= source[i].Weight > 0f;

                for (int j = 0; j < i; j++)
                {
                    if (source[j].Unit == source[i].Unit)
                    {
                        throw new ArgumentException($"단위 {source[i].Unit} 가 두 번 들어 있다.", paramName);
                    }
                }
            }

            if (!hasPositive)
            {
                throw new ArgumentException("가중치가 0보다 큰 단위가 하나 이상 있어야 한다.", paramName);
            }

            var copy = new PatternWeight[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
