using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 콤보 시작 신호: 콤보 예고가 시작되는 순간 증기 분출 + 경고음 1회. 단독 패턴에서는 나오지 않는다.
    /// 눈 색은 BossTelegraphView 가 신호 구간(BossFlowConfig 콤보 신호 선행 시간) 뒤에 켠다. spec: boss.md 9장 (T58)
    /// </summary>
    /// <remarks>증기·경고음 연결은 T59 (에디터). 비어 있으면 로그만 남긴다.</remarks>
    [DisallowMultipleComponent]
    public sealed class ComboSignalView : MonoBehaviour
    {
        [SerializeField] private BossFlowDriver _driver;

        [Tooltip("증기 파티클 (보스 자식에 배치한 것). 비어 있으면 생략")]
        [SerializeField] private ParticleSystem _steam;

        [Tooltip("경고음. 비어 있으면 생략")]
        [SerializeField] private AudioClip _warningClip;

        [Tooltip("경고음을 낼 AudioSource. 비우면 Awake 때 자신에서 찾는다")]
        [SerializeField] private AudioSource _audioSource;

        [SerializeField, Range(0f, 1f)] private float _warningVolume = 1f;

        private void Awake()
        {
            if (_driver == null)
            {
                GameLog.Error(this, "[ComboSignalView] BossFlowDriver 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            if (_audioSource == null)
            {
                TryGetComponent(out _audioSource);
            }
        }

        private void OnEnable()
        {
            if (_driver != null)
            {
                _driver.FlowStateChanged += OnFlowStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.FlowStateChanged -= OnFlowStateChanged;
            }
        }

        private void OnFlowStateChanged(BossFlowState state)
        {
            if (state != BossFlowState.Telegraph || !_driver.IsCurrentCombo)
            {
                return;
            }

            if (_steam != null)
            {
                _steam.Clear(true);
                _steam.Play(true);
            }

            if (_warningClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_warningClip, _warningVolume);
            }

            GameLog.Info(
                $"[ComboSignal] {_driver.CurrentUnit} 시작 신호 (증기 {(_steam != null ? "O" : "없음")}, " +
                $"경고음 {(_warningClip != null ? "O" : "없음")}, 눈 색까지 {_driver.CurrentSignalLead:F2}초)",
                this);
        }
    }
}
