using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    public class BossContext
    {
        public Transform BossTransform { get; }
        public Transform PlayerTransform { get; }
        public DamageHandler DamageHandler { get; }
        public BossBlackboard Blackboard { get; }

        public float CurrentHP => DamageHandler.CurrentHP;

        public volatile int DestroyedCoreCount = 0;
        public int TotalCoreCount { get; }

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

    [CreateAssetMenu(fileName = "BossPhase", menuName = "BossSystem/BossPhase")]
    public class BossPhase : ScriptableObject
    {
        [Header("Phase Trigger")]
        [SerializeField, Range(0f, 1f)] public float hpThreshold = 1f;
        [SerializeField, Range(0f, 1f)] public float hpRecoverRatio = 0f;
        [SerializeField] public int spawnPointIndex = -1;

        [Header("Patterns")]
        [SerializeField] public List<BossPatternBase> persistentPatterns = new();
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
