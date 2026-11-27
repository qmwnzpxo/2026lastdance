using UnityEngine;
using UnityEngine.UI;

namespace UnMachine.Boss
{
    /// <summary>
    /// 그로기 게이지 바 (T63 ✂️, 체력바 아래 얇은 바). 전투 시작 때 보이고 사망 때 숨긴다. spec: boss.md 15장
    /// </summary>
    /// <remarks>
    /// "낙섬!" 아이콘은 1주 축소로 삭제. 채움 방식 두 가지: Image(Type Filled, Horizontal) 또는
    /// SYS <c>UnifiedHealthBarVisual</c>(Chapter_1_Boss 체력바와 같은 셰이더 바 — 툴 "그로기 바 UI 만들기" 가 복제해 연결).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossGroggyBar : MonoBehaviour
    {
        [SerializeField] private BossGroggyDriver _groggy;
        [SerializeField] private BossController _boss;

        [Tooltip("켜고 끌 바 루트 (이 컴포넌트가 붙은 오브젝트와 달라도 됨)")]
        [SerializeField] private GameObject _root;

        [Tooltip("Image Type = Filled 인 채움 이미지 (아래 Visual 을 쓰면 비워도 됨)")]
        [SerializeField] private Image _fill;

        [Tooltip("SYS 체력바 비주얼 (Chapter_1_Boss 의 Boss HP 와 같은 셰이더 바). 지정하면 Fill 대신 이것을 채운다")]
        [SerializeField] private UnifiedHealthBarVisual _visual;

        private float _lastValue = -1f;

        private void Awake()
        {
            if (_groggy == null || _boss == null || _root == null || (_fill == null && _visual == null))
            {
                GameLog.Error(this, "[BossGroggyBar] 참조 누락 (Groggy·Boss·Root, Fill 또는 Visual) → 비활성화");
                enabled = false;
                return;
            }

            if (_root == gameObject)
            {
                GameLog.Warn("[BossGroggyBar] Root 가 자기 자신이면 숨긴 뒤 다시 켤 수 없음 → 자식 오브젝트를 지정", this);
            }

            _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_boss != null)
            {
                _boss.BattleStarted += Show;
                _boss.Died += Hide;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.BattleStarted -= Show;
                _boss.Died -= Hide;
            }
        }

        private void LateUpdate()
        {
            if (_root.activeSelf)
            {
                ApplyValue(_groggy.Normalized);
            }
        }

        private void Show()
        {
            _root.SetActive(true);

            // Visual 은 Root 아래라 켜지는 순간 Awake(머티리얼 생성) → 그 뒤에 설정한다.
            // 고스트 HP 는 끈다: 켜 두면 처음에 고스트가 100% → 0% 로 쓸려 내려간다.
            if (_visual != null)
            {
                _visual.SetGhostFill(false);
            }

            _lastValue = -1f;
            ApplyValue(_groggy.Normalized);
        }

        private void ApplyValue(float value)
        {
            if (Mathf.Approximately(value, _lastValue))
            {
                return;
            }

            _lastValue = value;
            if (_visual != null)
            {
                _visual.SetFill(value);
            }
            else
            {
                _fill.fillAmount = value;
            }
        }

        private void Hide()
        {
            _root.SetActive(false);
        }
    }
}
