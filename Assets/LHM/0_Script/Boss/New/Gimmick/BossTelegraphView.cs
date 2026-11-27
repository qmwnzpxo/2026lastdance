using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 예고(Telegraph) 동안 보스 스프라이트 색을 바꾼다: P2·P3 빨강 깜빡 2회 / P4~P6 노랑 / P1 하양.
    /// 1주 축소(T39 ✂️): 눈 오버레이 대신 스프라이트 전체 틴트. spec: boss.md 9장
    /// 예고 밖에서는 페이즈 틴트: 1P 원래 색, 페이즈 전환 시작부터 2P 내내 붉은 틴트 (T34 ✂️, spec: boss.md 8장 전환 3).
    /// </summary>
    /// <remarks>
    /// 스프라이트 색을 바꾸는 곳은 이 컴포넌트 하나다 (예고 색·페이즈 틴트가 서로 덮어쓰지 않게).
    /// 예고 중에는 페이즈 틴트 대신 예고 색만 곱한다 → 2P 에서도 예고 색이 붉게 섞이지 않는다 ("눈은 예고 전용").
    /// SpriteRenderer.color 는 곱셈이라 순수 하양은 변화가 없다 → "하양" 기본값은 옅은 하늘색.
    /// 피격 깜빡임(BossController)은 enabled 를 끄고 켜므로 색과 충돌하지 않는다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossTelegraphView : MonoBehaviour
    {
        [SerializeField] private BossFlowDriver _driver;

        [Tooltip("색을 바꿀 스프라이트. 비워 두면 Awake 때 자신·자식에서 찾는다")]
        [SerializeField] private SpriteRenderer[] _renderers;

        [Header("예고 색 (곱셈 틴트)")]
        [SerializeField] private Color _red = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private Color _yellow = new Color(1f, 0.9f, 0.3f);
        [SerializeField] private Color _white = new Color(0.6f, 0.85f, 1f);

        [Header("페이즈 틴트 (T34 ✂️)")]
        [Tooltip("페이즈 전환 시작부터 2P 내내 곱하는 색. 〔임시값〕 기획서 '붉은 틴트'")]
        [SerializeField] private Color _phase2Tint = new Color(1f, 0.55f, 0.55f);

        private BossController _boss;
        private bool _isPhaseTinted;
        private Color[] _baseColors;
        private TelegraphSignal _signal;
        private float _duration;
        private float _lead;
        private bool _isTelegraphing;
        private bool _isLit;

        private void Awake()
        {
            if (_driver == null)
            {
                GameLog.Error(this, "[BossTelegraphView] BossFlowDriver 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
            }

            // Driver 는 BossController 와 같은 오브젝트에 둔다 (BossFlowDriver remarks)
            if (!_driver.TryGetComponent(out _boss))
            {
                GameLog.Warn("[BossTelegraphView] Driver 오브젝트에 BossController 없음 → 2P 틴트는 전환 시작에만 켜짐", this);
            }
        }

        private void OnEnable()
        {
            if (_driver != null)
            {
                _driver.FlowStateChanged += OnFlowStateChanged;
            }

            if (_boss != null)
            {
                _boss.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.FlowStateChanged -= OnFlowStateChanged;
            }

            if (_boss != null)
            {
                _boss.PhaseChanged -= OnPhaseChanged;
            }

            EndTelegraph();
        }

        private void OnPhaseChanged(int phase)
        {
            SetPhaseTint(phase >= 2);
        }

        /// <summary>페이즈 틴트를 켜고 끈다. 예고 색이 켜져 있으면 예고가 끝날 때 반영된다.</summary>
        private void SetPhaseTint(bool isTinted)
        {
            if (_isPhaseTinted == isTinted)
            {
                return;
            }

            _isPhaseTinted = isTinted;
            GameLog.Info($"[BossTelegraphView] 2P 붉은 틴트 {(isTinted ? "On" : "Off")}", this);
            if (!_isLit)
            {
                ApplyColor(false);
            }
        }

        private void LateUpdate()
        {
            if (!_isTelegraphing)
            {
                return;
            }

            // 경과 = 예고 길이 - 남은 시간 → World 채널을 따른다 (히트스톱 중 멈춤).
            // 콤보는 앞 _lead 초가 증기·경고음 구간 → 그 뒤부터 색 (T58, spec: boss.md 9장 "눈 색 예고보다 먼저")
            float elapsed = _duration - _driver.StateRemaining - _lead;
            bool isLit = elapsed >= 0f && _signal.IsLit(elapsed, _duration - _lead);
            if (isLit != _isLit)
            {
                _isLit = isLit;
                ApplyColor(isLit);
            }
        }

        private void OnFlowStateChanged(BossFlowState state)
        {
            if (state == BossFlowState.Telegraph)
            {
                _signal = TelegraphSignal.For(_driver.CurrentUnit);
                _duration = _driver.StateRemaining;
                _lead = Mathf.Min(_driver.CurrentSignalLead, _duration);
                _isTelegraphing = true;
                _isLit = _lead <= 0f;
                ApplyColor(_isLit);
                return;
            }

            // spec: boss.md 8장 전환 3 "붉은 틴트로 물들이고 2페이즈 동안 유지" — 전환 연출 시작부터
            if (state == BossFlowState.PhaseTransition)
            {
                SetPhaseTint(true);
            }

            EndTelegraph();
        }

        private void EndTelegraph()
        {
            if (!_isTelegraphing)
            {
                return;
            }

            _isTelegraphing = false;
            _isLit = false;
            ApplyColor(false);
        }

        private void ApplyColor(bool isLit)
        {
            if (_baseColors == null)
            {
                return;
            }

            // 예고 중 = 예고 색만, 그 외 = 페이즈 틴트 (1P 는 원래 색)
            Color tint = isLit ? GetTint(_signal.Color) : (_isPhaseTinted ? _phase2Tint : Color.white);
            for (int i = 0; i < _renderers.Length; i++)
            {
                SpriteRenderer target = _renderers[i];
                if (target != null)
                {
                    target.color = _baseColors[i] * tint;
                }
            }
        }

        private Color GetTint(TelegraphColor color)
        {
            switch (color)
            {
                case TelegraphColor.Red: return _red;
                case TelegraphColor.Yellow: return _yellow;
                default: return _white;
            }
        }
    }
}
