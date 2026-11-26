using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 그로기 게이지 수치 (boss.md 7장, 1주 축소 T60 ✂️). Groggy 시간·배율은 BossFlowConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "UnMachine/Config/Groggy", fileName = "GroggyConfig")]
    public sealed class GroggyConfig : ScriptableObject
    {
        [Tooltip("게이지 최대. spec: boss.md 4장 100")]
        [SerializeField, Min(1f)] private float _max = 100f;

        [Tooltip("돌진 벽 충돌 증가량. spec: boss.md 7장 +25")]
        [SerializeField, Min(0f)] private float _wallHitAmount = 25f;

        [Tooltip("받은 피해 1당 증가량 (배율 적용 전 피해). 〔임시값〕 축소안: 일섬 +10·일반 +1 대신 피해 비례")]
        [SerializeField, Min(0f)] private float _damageToGauge = 0.5f;

        [Tooltip("마지막 증가 후 감소 시작까지(초). spec: boss.md 7장 3초")]
        [SerializeField, Min(0f)] private float _decayDelaySeconds = 3f;

        [Tooltip("초당 감소량. spec: boss.md 7장 5")]
        [SerializeField, Min(0f)] private float _decayPerSecond = 5f;

        /// <summary>돌진 벽 충돌 증가량.</summary>
        public float WallHitAmount => _wallHitAmount;

        /// <summary>받은 피해 1당 증가량.</summary>
        public float DamageToGauge => _damageToGauge;

        /// <summary>이 값으로 Core 게이지를 만든다.</summary>
        public GroggyGauge CreateGauge()
        {
            return new GroggyGauge(_max, _decayDelaySeconds, _decayPerSecond);
        }
    }
}
