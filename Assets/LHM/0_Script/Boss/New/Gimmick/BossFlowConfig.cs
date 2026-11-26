using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 보스 행동 상태 머신(BossFlowMachine) 수치. 현민이 인스펙터에서 튜닝한다.
    /// 초기값 = boss.md 임시 수치. 〔임시값〕 표시는 기획서 확정 시 다시 확인.
    /// </summary>
    [CreateAssetMenu(menuName = "UnMachine/Config/Boss Flow", fileName = "BossFlowConfig")]
    public sealed class BossFlowConfig : ScriptableObject
    {
        [Header("휴식")]
        [Tooltip("1페이즈 휴식(초). spec: boss.md 5장 Resting")]
        [SerializeField, Min(0f)] private float _restSecondsPhase1 = 1.5f;

        [Tooltip("2페이즈 휴식(초). spec: boss.md 5장 Resting, 8장 2페이즈")]
        [SerializeField, Min(0f)] private float _restSecondsPhase2 = 0.8f;

        [Header("휴식 걷기 (BossLocomotion, T32)")]
        [Tooltip("Resting 중 플레이어 쪽으로 걷는 속도 (Unit/초). spec: boss.md 4장 걷는 속도 3, 5장 Resting")]
        [SerializeField, Min(0f)] private float _restWalkSpeed = 3f;

        [Tooltip("플레이어와 x 거리가 이 값(Unit) 이하면 정지. 〔임시값〕 기획서 값 없음 — BACKLOG T32·기존 RestingState 1.5")]
        [SerializeField, Min(0f)] private float _restWalkStopDistance = 1.5f;

        [Header("예고·반격 틈")]
        [Tooltip("예고 최소 길이(초). 패턴 예고가 더 짧아도 이 값 보장. spec: boss.md 9장")]
        [SerializeField, Min(0f)] private float _minTelegraphSeconds = 0.4f;

        [Tooltip("2페이즈 반격 틈 배율 (패턴 값 × 이 값). spec: boss.md 8장 2페이즈")]
        [SerializeField, Min(0f)] private float _phase2PunishScale = 0.8f;

        [Tooltip("콤보 예고 앞에 붙는 증기·경고음 구간(초). 이 동안 눈 색 없음. 〔임시값〕 spec: boss.md 9장 '눈 색 예고보다 먼저'")]
        [SerializeField, Min(0f)] private float _comboSignalLeadSeconds = 0.3f;

        [Header("인터럽트 상태")]
        [Tooltip("벽 충돌 기절 길이(초). 끝나면 Resting. spec: boss.md 5장 Stunned")]
        [SerializeField, Min(0f)] private float _stunnedSeconds = 1.5f;

        [Tooltip("그로기 길이(초). 끝나면 Resting. spec: boss.md 4장·5장 Groggy")]
        [SerializeField, Min(0f)] private float _groggySeconds = 4f;

        [Tooltip("페이즈 전환 길이(초). 끝나면 2페이즈 Idle. 〔임시값〕 기획서 값 없음, 현민 승인 2026-11-25")]
        [SerializeField, Min(0f)] private float _phaseTransitionSeconds = 1.5f;

        [Header("받는 피해 배율 〔임시값: 3단계〕")]
        [Tooltip("기본 배율. spec: boss.md 4장")]
        [SerializeField, Min(0f)] private float _normalDamageScale = 1f;

        [Tooltip("Punishable·Stunned 배율. spec: boss.md 4장, 17장 #12")]
        [SerializeField, Min(0f)] private float _vulnerableDamageScale = 1.2f;

        [Tooltip("Groggy 배율. spec: boss.md 4장")]
        [SerializeField, Min(0f)] private float _groggyDamageScale = 1.5f;

        /// <summary>Resting 걷기 속도 (Unit/초).</summary>
        public float RestWalkSpeed => _restWalkSpeed;

        /// <summary>Resting 걷기 정지 거리 (플레이어와 x 거리, Unit).</summary>
        public float RestWalkStopDistance => _restWalkStopDistance;

        /// <summary>콤보 시작 신호 구간(초). 콤보 예고 = 이 값 + 콤보 예고 값 (T58).</summary>
        public float ComboSignalLeadSeconds => _comboSignalLeadSeconds;

        /// <summary>Core 상태 머신에 넘길 수치 묶음을 만든다.</summary>
        public BossFlowSettings ToSettings()
        {
            var damageScale = new BossDamageScale(_normalDamageScale, _vulnerableDamageScale, _groggyDamageScale);

            return new BossFlowSettings(
                _restSecondsPhase1,
                _restSecondsPhase2,
                _minTelegraphSeconds,
                _phase2PunishScale,
                _stunnedSeconds,
                _groggySeconds,
                _phaseTransitionSeconds,
                damageScale);
        }
    }
}
