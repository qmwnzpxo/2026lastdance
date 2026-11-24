using System;

namespace UnMachine.Core
{
    /// <summary>
    /// System.Random 기반 IRandom 기본 구현. 스레드 안전하지 않다(메인 스레드 전용).
    /// </summary>
    public sealed class SystemRandom : IRandom
    {
        private readonly Random _random;

        /// <summary>시드를 고정해서 생성한다. 같은 시드 = 같은 난수열.</summary>
        public SystemRandom(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>시간 기반 시드로 생성한다. 재현이 필요 없을 때만 쓴다.</summary>
        public SystemRandom()
        {
            _random = new Random();
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), $"max({maxExclusive})는 min({minInclusive})보다 커야 한다.");
            }

            return _random.Next(minInclusive, maxExclusive);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxInclusive), $"max({maxInclusive})는 min({minInclusive}) 이상이어야 한다.");
            }

            return minInclusive + (float)_random.NextDouble() * (maxInclusive - minInclusive);
        }

        public float Value01 => (float)_random.NextDouble();
    }
}
