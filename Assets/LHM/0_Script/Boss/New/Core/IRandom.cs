namespace UnMachine.Core
{
    /// <summary>
    /// 난수 공급자. Core 로직은 UnityEngine.Random 대신 이 인터페이스를 주입받는다.
    /// 시드를 고정하면 씬 테스트에서 같은 상황(판넬 경로, 커맨드 시퀀스 등)을 다시 만들 수 있다.
    /// </summary>
    public interface IRandom
    {
        /// <summary>[minInclusive, maxExclusive) 범위의 정수를 반환한다.</summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>[minInclusive, maxInclusive] 범위의 실수를 반환한다.</summary>
        float Range(float minInclusive, float maxInclusive);

        /// <summary>[0, 1) 범위의 실수를 반환한다.</summary>
        float Value01 { get; }
    }
}
