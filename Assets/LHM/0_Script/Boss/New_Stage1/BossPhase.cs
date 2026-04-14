using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    /// <summary>
    /// 패턴이 보스 상태를 읽고 쓸 수 있는 컨텍스트 객체.
    /// 패턴 클래스가 BossController를 직접 참조하지 않도록 의존성을 역전시켜
    /// 결합도(Coupling)를 낮추고 패턴 독립적 테스트를 가능하게 한다.
    /// </summary>
    public class BossContext
    {
        // ── READ-ONLY ─────────────────────────────────────────────────────
        public Transform BossTransform { get; }
        public Transform PlayerTransform { get; }
        public DamageHandler DamageHandler { get; }

        /// <summary>
        /// BossBlackboard 직접 접근 (PlayerVelocity, HP 이벤트 등).
        /// 패턴이 Blackboard에 직접 의존해야 할 때만 사용.
        /// 가능하면 Context 프로퍼티로 간접 접근을 권장.
        /// </summary>
        public BossBlackboard Blackboard { get; }

        public float CurrentHP => DamageHandler.CurrentHP;

        // ── READ / WRITE (패턴 간 공유) ───────────────────────────────────

        /// <summary>
        /// 파괴된 레이저 코어 수.
        /// Phase1LaserPattern이 쓰고, Phase1ShieldPattern이 읽는다.
        /// volatile: 멀티프레임 가시성 보장 (UniTask 단일 스레드이므로 충분).
        /// </summary>
        public volatile int DestroyedCoreCount = 0;

        /// <summary>레이저 코어 총 수 (기획: 4개)</summary>
        public int TotalCoreCount { get; }

        // ─────────────────────────────────────────────────────────────────

        public BossContext(
            Transform bossTransform,
            Transform playerTransform,
            DamageHandler damageHandler,
            BossBlackboard blackboard,
            int totalCoreCount = 4)
        {
            BossTransform = bossTransform;
            PlayerTransform = playerTransform;
            DamageHandler = damageHandler;
            Blackboard = blackboard;
            TotalCoreCount = totalCoreCount;
        }
    }

    /// <summary>
    /// 하나의 보스 페이즈를 정의하는 데이터 컨테이너.
    /// ScriptableObject로 만들어 각 페이즈를 에셋으로 분리 관리.
    /// 
    /// 패턴 실행 방식:
    ///   - PersistentPatterns: WhenAll로 묶어 페이즈 내내 병렬로 구동
    ///   - SequentialPatterns: 순서대로 하나씩 await 실행
    ///   두 그룹은 다시 WhenAll로 묶여 동시에 진행된다.
    /// </summary>
    [CreateAssetMenu(fileName = "BossPhase", menuName = "BossSystem/BossPhase")]
    public class BossPhase : ScriptableObject
    {
        [Header("Phase Trigger")]
        [Tooltip("이 페이즈로 전환되는 HP 임계값 (0~1 정규화). 1.0 = 최초 페이즈.")]
        [SerializeField, Range(0f, 1f)] public float hpThreshold = 1f;

        [Tooltip("페이즈 시작 시 체력을 이 비율로 회복. 0이면 회복 없음.")]
        [SerializeField, Range(0f, 1f)] public float hpRecoverRatio = 0f;

        [Tooltip("페이즈 시작 시 보스가 이동할 스폰 포인트 인덱스. -1이면 이동 없음.")]
        [SerializeField] public int spawnPointIndex = -1;

        [Header("Patterns — run concurrently within phase")]
        [Tooltip("페이즈 내내 지속적으로 실행되는 패턴. (방어막, 루프 레이저 등)")]
        [SerializeField] public List<BossPatternBase> persistentPatterns = new();

        [Tooltip("순서대로 한 번씩 실행되는 패턴. 마지막 패턴 완료 후 처음으로 루프.")]
        [SerializeField] public List<BossPatternBase> sequentialPatterns = new();

        /// <summary>유효성 검사. 에디터 저장 시 자동 호출.</summary>
        private void OnValidate()
        {
            foreach (var p in persistentPatterns)
            {
                if (p != null && p.Type != PatternType.Persistent)
                    Debug.LogWarning($"[BossPhase] '{p.PatternId}' is not a Persistent pattern but placed in persistentPatterns list!", this);
            }
            foreach (var p in sequentialPatterns)
            {
                if (p != null && p.Type != PatternType.Sequential)
                    Debug.LogWarning($"[BossPhase] '{p.PatternId}' is not a Sequential pattern but placed in sequentialPatterns list!", this);
            }
        }
    }
}
