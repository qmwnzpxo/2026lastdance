//using System.Collections.Generic;
//using UnityEngine;

//namespace BossSystem
//{
//    // ─────────────────────────────────────────────────────────────────────
//    // BossContext
//    // ─────────────────────────────────────────────────────────────────────
//    public class BossContext
//    {
//        public Transform BossTransform { get; }
//        public Transform PlayerTransform { get; }
//        public DamageHandler DamageHandler { get; }
//        public BossBlackboard Blackboard { get; }
//        public LineRenderer lineRenderer { get; }

//        public float CurrentHP => DamageHandler.CurrentHP;

//        public volatile int DestroyedCoreCount = 0;
//        public int TotalCoreCount { get; }

//        // ── 판넬 패턴 클리어 플래그 ──────────────────────────────────────
//        // Phase1PanelPattern이 발사 횟수를 모두 소진하면 SetPanelPatternCleared() 호출.
//        // BossController.WatchPhaseTransitionAsync가 페이즈 전환 조건으로 사용.
//        // EnterPhaseAsync 진입 시 ResetPanelPatternCleared()로 초기화.
//        public bool IsPanelPatternCleared { get; private set; } = false;
//        public void SetPanelPatternCleared() => IsPanelPatternCleared = true;
//        public void ResetPanelPatternCleared() => IsPanelPatternCleared = false;

//        public BossContext(
//            Transform bossTransform,
//            Transform playerTransform,
//            DamageHandler damageHandler,
//            BossBlackboard blackboard,
//            int totalCoreCount = 4)
//        {
//            BossTransform = bossTransform;
//            PlayerTransform = playerTransform;
//            DamageHandler = damageHandler;
//            Blackboard = blackboard;
//            TotalCoreCount = totalCoreCount;
//        }
//    }

//    // ─────────────────────────────────────────────────────────────────────
//    // BossPhase
//    // ─────────────────────────────────────────────────────────────────────
//    [CreateAssetMenu(fileName = "BossPhase", menuName = "BossSystem/BossPhase")]
//    public class BossPhase : ScriptableObject
//    {
//        [Header("Phase Trigger")]
//        [SerializeField, Range(0f, 1f)] public float hpThreshold = 1f;
//        [SerializeField, Range(0f, 1f)] public float hpRecoverRatio = 0f;
//        [SerializeField] public int spawnPointIndex = -1;

//        [Header("Patterns")]
//        [SerializeField] public List<BossPatternBase> persistentPatterns = new();
//        [SerializeField] public List<BossPatternBase> sequentialPatterns = new();

//        private void OnValidate()
//        {
//            foreach (var p in persistentPatterns)
//                if (p != null && p.Type != PatternType.Persistent)
//                    Debug.LogWarning(
//                        $"[BossPhase] '{p.PatternId}' is Persistent 타입이 아닌데 " +
//                        $"persistentPatterns 에 배치되었습니다.", this);

//            foreach (var p in sequentialPatterns)
//                if (p != null && p.Type != PatternType.Sequential)
//                    Debug.LogWarning(
//                        $"[BossPhase] '{p.PatternId}' is Sequential 타입이 아닌데 " +
//                        $"sequentialPatterns 에 배치되었습니다.", this);
//        }
//    }
//}
