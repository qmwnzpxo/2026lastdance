using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // BossPhase.cs  —  BossContext + BossPhase 통합
    //
    // ── 중복 제거 내역 ─────────────────────────────────────────────────────
    //   Before: BossContext가 BossPhase.cs와 BossContext_Mirror.cs 양쪽에
    //           각각 선언되어 있었고, BossContext_Mirror.cs는 빈 선언부였음.
    //   After:  BossContext_Mirror.cs 삭제. 이 파일이 단일 정의 소스.
    //           Mirror 컴포넌트는 Mirror.cs (BossContext_Mirror.cs에서 분리)
    //           로 유지하되, BossContext 중복 선언부는 제거.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 패턴이 보스 상태를 읽고 쓸 수 있는 경량 계약(Contract).
    ///
    /// ── 의존성 역전 설계 ────────────────────────────────────────────────
    ///   패턴이 BossController를 직접 참조하면 결합도가 높아진다.
    ///   BossContext는 패턴이 필요한 최소 인터페이스만 노출한다.
    ///
    /// ── 스레드 안전성 ───────────────────────────────────────────────────
    ///   UniTask는 Unity PlayerLoop 기반 단일 스레드 스케줄러이므로
    ///   volatile int로 가시성만 보장하면 충분 (원자성 불필요).
    /// </summary>
    public class BossContext
    {
        // ── READ-ONLY ─────────────────────────────────────────────────────
        public Transform     BossTransform   { get; }
        public Transform     PlayerTransform { get; }
        public DamageHandler DamageHandler   { get; }

        /// <summary>
        /// BossBlackboard 직접 접근 (PlayerVelocity, HP 이벤트 등).
        /// 가능하면 Context 프로퍼티로 간접 접근을 권장.
        /// </summary>
        public BossBlackboard Blackboard { get; }

        public float CurrentHP => DamageHandler.CurrentHP;

        // ── READ / WRITE (패턴 간 공유) ───────────────────────────────────

        /// <summary>
        /// 파괴된 레이저 코어 수.
        /// Phase1LaserPattern이 쓰고, Phase1ShieldPattern이 읽는다.
        /// volatile: 멀티프레임 가시성 보장.
        /// </summary>
        public volatile int DestroyedCoreCount = 0;

        /// <summary>레이저 코어 총 수 (기획: 4개)</summary>
        public int TotalCoreCount { get; }

        public BossContext(
            Transform     bossTransform,
            Transform     playerTransform,
            DamageHandler damageHandler,
            BossBlackboard blackboard,
            int totalCoreCount = 4)
        {
            BossTransform   = bossTransform;
            PlayerTransform = playerTransform;
            DamageHandler   = damageHandler;
            Blackboard      = blackboard;
            TotalCoreCount  = totalCoreCount;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BossPhase  —  페이즈 데이터 컨테이너
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 하나의 보스 페이즈를 정의하는 데이터 컨테이너 (ScriptableObject).
    ///
    /// 패턴 실행 방식:
    ///   - persistentPatterns : WhenAll로 페이즈 내내 병렬 구동
    ///   - sequentialPatterns : 순서대로 await, 마지막 완료 후 루프
    ///   두 그룹은 다시 WhenAll로 묶여 동시에 진행.
    /// </summary>
    [CreateAssetMenu(fileName = "BossPhase", menuName = "BossSystem/BossPhase")]
    public class BossPhase : ScriptableObject
    {
        [Header("Phase Trigger")]
        [Tooltip("이 페이즈로 전환되는 HP 임계값 (0~1). 1.0 = 최초 페이즈.")]
        [SerializeField, Range(0f, 1f)] public float hpThreshold    = 1f;

        [Tooltip("페이즈 시작 시 HP를 이 비율로 회복. 0이면 없음.")]
        [SerializeField, Range(0f, 1f)] public float hpRecoverRatio = 0f;

        [Tooltip("페이즈 시작 시 보스가 이동할 스폰 포인트 인덱스. -1이면 이동 없음.")]
        [SerializeField] public int spawnPointIndex = -1;

        [Header("Patterns")]
        [Tooltip("페이즈 내내 지속 실행. (방어막, 360° 레이저 등)")]
        [SerializeField] public List<BossPatternBase> persistentPatterns = new();

        [Tooltip("순서대로 한 번씩 실행 후 루프.")]
        [SerializeField] public List<BossPatternBase> sequentialPatterns = new();

        private void OnValidate()
        {
            foreach (var p in persistentPatterns)
                if (p != null && p.Type != PatternType.Persistent)
                    Debug.LogWarning(
                        $"[BossPhase] '{p.PatternId}' is Persistent 타입이 아닌데 " +
                        $"persistentPatterns 에 배치되었습니다.", this);

            foreach (var p in sequentialPatterns)
                if (p != null && p.Type != PatternType.Sequential)
                    Debug.LogWarning(
                        $"[BossPhase] '{p.PatternId}' is Sequential 타입이 아닌데 " +
                        $"sequentialPatterns 에 배치되었습니다.", this);
        }
    }
}
